using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Bloxstrap.Models.APIs.GitHub;

namespace Bloxstrap
{
    internal sealed record UpdateCheckResult(GithubRelease? Release, bool NoPublishedRelease);

    internal static class UpdateManager
    {
        private const long MaximumExecutableSize = 256L * 1024 * 1024;
        private const long MaximumChecksumSize = 4096;

        private static string LatestReleaseUrl =>
            $"https://api.github.com/repos/{App.ProjectRepository}/releases/latest";

        public static async Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (!String.Equals(App.ProjectName, "ClaudeTrap", StringComparison.Ordinal))
                throw new InvalidOperationException("Automatic updates are not supported by QA builds.");

            using HttpResponseMessage response = await App.HttpClient.GetAsync(
                LatestReleaseUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResult(null, true);

            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            GithubRelease? release = await JsonSerializer.DeserializeAsync<GithubRelease>(
                stream,
                cancellationToken: cancellationToken
            );

            if (release is null || String.IsNullOrWhiteSpace(release.TagName) || release.Assets is null)
                throw new InvalidDataException("GitHub returned an incomplete latest-release response.");

            try
            {
                _ = Utilities.GetVersionFromString(release.TagName);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("GitHub returned an invalid release version tag.", ex);
            }

            return new UpdateCheckResult(release, false);
        }

        public static bool IsNewerRelease(GithubRelease release)
        {
            if (release.Draft || release.Prerelease || String.IsNullOrWhiteSpace(release.TagName))
                return false;

            try
            {
                Version latestVersion = Utilities.GetVersionFromString(release.TagName);
                Version currentVersion = Utilities.GetVersionFromString(App.Version);
                return latestVersion > currentVersion;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("UpdateManager::IsNewerRelease", ex);
                return false;
            }
        }

        public static bool HasRequiredAssets(GithubRelease release)
        {
            GithubReleaseAsset? executable = release.Assets?.FirstOrDefault(
                x => String.Equals(x.Name, "ClaudeTrap.exe", StringComparison.OrdinalIgnoreCase)
            );
            GithubReleaseAsset? checksum = release.Assets?.FirstOrDefault(
                x => String.Equals(x.Name, "ClaudeTrap.exe.sha256", StringComparison.OrdinalIgnoreCase)
            );

            return executable is not null
                && checksum is not null
                && IsSafeDownloadUrl(executable.BrowserDownloadUrl)
                && IsSafeDownloadUrl(checksum.BrowserDownloadUrl);
        }

        public static async Task<string> DownloadReleaseAsync(
            GithubRelease release,
            CancellationToken cancellationToken = default
        )
        {
            if (!IsNewerRelease(release))
                throw new InvalidDataException("The selected GitHub release is not newer than this installation.");

            if (!HasRequiredAssets(release))
                throw new InvalidDataException("The GitHub release is missing ClaudeTrap.exe or its SHA-256 checksum.");

            Version releaseVersion = Utilities.GetVersionFromString(release.TagName);
            string updateDirectory = Path.Combine(Paths.TempUpdates, releaseVersion.ToString());
            Directory.CreateDirectory(updateDirectory);

            string executablePath = Path.Combine(updateDirectory, "ClaudeTrap.exe");
            string checksumPath = Path.Combine(updateDirectory, "ClaudeTrap.exe.sha256");

            List<GithubReleaseAsset> assets = release.Assets
                ?? throw new InvalidDataException("The GitHub release does not contain an asset list.");

            GithubReleaseAsset executableAsset = assets.First(
                x => String.Equals(x.Name, "ClaudeTrap.exe", StringComparison.OrdinalIgnoreCase)
            );
            GithubReleaseAsset checksumAsset = assets.First(
                x => String.Equals(x.Name, "ClaudeTrap.exe.sha256", StringComparison.OrdinalIgnoreCase)
            );

            try
            {
                await DownloadAssetAsync(
                    executableAsset,
                    executablePath,
                    MaximumExecutableSize,
                    cancellationToken
                );

                await DownloadAssetAsync(
                    checksumAsset,
                    checksumPath,
                    MaximumChecksumSize,
                    cancellationToken
                );

                string checksumText = (await File.ReadAllTextAsync(checksumPath, cancellationToken)).Trim();
                string[] checksumParts = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                string expectedHashText = checksumParts.FirstOrDefault() ?? "";

                if (expectedHashText.Length != 64 || !expectedHashText.All(Uri.IsHexDigit))
                    throw new InvalidDataException("The release SHA-256 checksum file is malformed.");

                byte[] expectedHash = Convert.FromHexString(expectedHashText);

                await using (FileStream executableStream = File.OpenRead(executablePath))
                {
                    byte[] actualHash = await SHA256.HashDataAsync(executableStream, cancellationToken);
                    if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                        throw new InvalidDataException("The downloaded executable failed SHA-256 verification.");
                }

                await using (FileStream executableStream = File.OpenRead(executablePath))
                {
                    byte[] signature = new byte[2];
                    int bytesRead = await executableStream.ReadAsync(signature.AsMemory(0, 2), cancellationToken);

                    if (bytesRead != 2 || signature[0] != 0x4D || signature[1] != 0x5A)
                        throw new InvalidDataException("The downloaded file is not a valid Windows executable.");
                }

                string? productVersion = FileVersionInfo.GetVersionInfo(executablePath).ProductVersion;
                if (String.IsNullOrWhiteSpace(productVersion)
                    || Utilities.CompareVersions(productVersion, release.TagName) != VersionComparison.Equal)
                {
                    throw new InvalidDataException("The downloaded executable version does not match the GitHub release tag.");
                }

                return executablePath;
            }
            catch
            {
                TryDelete(executablePath);
                throw;
            }
            finally
            {
                TryDelete(checksumPath);
            }
        }

        private static async Task DownloadAssetAsync(
            GithubReleaseAsset asset,
            string destinationPath,
            long maximumSize,
            CancellationToken cancellationToken
        )
        {
            if (asset.Size <= 0 || asset.Size > maximumSize)
                throw new InvalidDataException($"The release asset '{asset.Name}' has an invalid size.");

            if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? downloadUri)
                || downloadUri.Scheme != Uri.UriSchemeHttps
                || !String.Equals(downloadUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The release asset '{asset.Name}' does not use a trusted GitHub download URL.");
            }

            string partialPath = destinationPath + ".partial";

            try
            {
                using HttpResponseMessage response = await App.UpdateHttpClient.GetAsync(
                    downloadUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                response.EnsureSuccessStatusCode();

                Uri? finalUri = response.RequestMessage?.RequestUri;
                if (finalUri is null || finalUri.Scheme != Uri.UriSchemeHttps
                    || !(String.Equals(finalUri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(finalUri.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(finalUri.Host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"The release asset '{asset.Name}' redirected to an untrusted host.");

                long? contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value != asset.Size)
                    throw new InvalidDataException($"The release asset '{asset.Name}' has an unexpected content length.");

                await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
                long writtenLength;

                await using (var destination = new FileStream(
                    partialPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true
                ))
                {
                    await source.CopyToAsync(destination, 81920, cancellationToken);
                    await destination.FlushAsync(cancellationToken);
                    writtenLength = destination.Length;
                }

                if (writtenLength != asset.Size)
                    throw new InvalidDataException($"The downloaded release asset '{asset.Name}' is incomplete.");

                File.Move(partialPath, destinationPath, overwrite: true);
            }
            finally
            {
                TryDelete(partialPath);
            }
        }

        private static bool IsSafeDownloadUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("UpdateManager::TryDelete", ex);
            }
        }
    }
}

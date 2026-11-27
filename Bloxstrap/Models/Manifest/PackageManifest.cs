/*
 * Roblox Studio Mod Manager (ProjectSrc/Utility/PackageManifest.cs)
 * MIT License
 * Copyright (c) 2015-present MaximumADHD
*/

namespace Bloxstrap.Models.Manifest
{
    public class PackageManifest : List<Package>
    {
        public PackageManifest(string data)
        {
            using var reader = new StringReader(data);
            string? version = reader.ReadLine();

            if (version != "v0")
                throw new NotSupportedException($"Unexpected package manifest version: {version} (expected v0).");

            var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                string? fileName = reader.ReadLine();
                if (fileName is null)
                    break;

                if (fileName == "RobloxPlayerLauncher.exe")
                    break;

                string? signature = reader.ReadLine();
                string? rawPackedSize = reader.ReadLine();
                string? rawSize = reader.ReadLine();

                if (String.IsNullOrWhiteSpace(fileName)
                    || String.IsNullOrWhiteSpace(signature)
                    || String.IsNullOrWhiteSpace(rawPackedSize)
                    || String.IsNullOrWhiteSpace(rawSize))
                    throw new InvalidDataException("Roblox package manifest contains an incomplete package entry.");

                if (!Regex.IsMatch(fileName, "^[A-Za-z0-9][A-Za-z0-9._-]*$"))
                    throw new InvalidDataException($"Roblox package manifest contains an unsafe package name: '{fileName}'.");

                if (!Regex.IsMatch(signature, "^[0-9a-fA-F]{32}$"))
                    throw new InvalidDataException($"Roblox package '{fileName}' contains an invalid MD5 signature.");

                if (!Int32.TryParse(rawPackedSize, NumberStyles.None, CultureInfo.InvariantCulture, out int packedSize)
                    || !Int32.TryParse(rawSize, NumberStyles.None, CultureInfo.InvariantCulture, out int size)
                    || packedSize <= 0
                    || size <= 0)
                    throw new InvalidDataException($"Roblox package '{fileName}' contains invalid size metadata.");

                if (!packageNames.Add(fileName))
                    throw new InvalidDataException($"Roblox package manifest contains duplicate entry '{fileName}'.");

                Add(new Package
                {
                    Name = fileName,
                    Signature = signature.ToLowerInvariant(),
                    PackedSize = packedSize,
                    Size = size
                });
            }

            if (Count == 0)
                throw new InvalidDataException("Roblox package manifest did not contain any installable packages.");
        }
    }
}

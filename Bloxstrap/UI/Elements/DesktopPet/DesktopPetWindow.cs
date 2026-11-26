using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Bloxstrap.UI.Elements.DesktopPet
{
    /// <summary>
    /// Small, independent desktop companion. The window is transparent and topmost,
    /// while all animation and movement are kept on the WPF dispatcher.
    /// </summary>
    public sealed class DesktopPetWindow : Window
    {
        private const double PetSize = 104;
        private const double Gravity = 1050;
        private const double ScreenMargin = 8;

        private readonly Canvas _sprite2D;
        private readonly Viewport3D _sprite3D;
        private readonly ScaleTransform _spriteScale;
        private readonly RotateTransform _spriteTilt;
        private readonly AxisAngleRotation3D _modelRotation;
        private readonly DispatcherTimer _timer;
        private readonly Random _random = new();

        private List<PetPlatform> _platforms = new();
        private PetPlatform? _currentPlatform;
        private PetPlatform? _targetPlatform;

        private PetMotionState _motion = PetMotionState.Walking;
        private bool _paused;
        private bool _dragging;
        private bool _use3D;
        private Point _dragOffset;
        private Point _lastPointer;
        private DateTime _lastPointerTime;
        private DateTime _nextWindowRefresh;
        private DateTime _nextTrick;
        private double _velocityX;
        private double _velocityY;
        private double _direction = 1;
        private double _climbSideX;
        private DateTime _lastTick = DateTime.UtcNow;

        private double WalkSpeed => 76 * Math.Clamp(App.Settings.Prop.DesktopPetSpeed, 0.5, 2.0);

        public DesktopPetWindow()
        {
            Title = "Claude desktop pet";
            Width = PetSize;
            Height = PetSize;
            MinWidth = PetSize;
            MinHeight = PetSize;
            MaxWidth = PetSize;
            MaxHeight = PetSize;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SnapsToDevicePixels = true;

            _spriteScale = new ScaleTransform(1, 1);
            _spriteTilt = new RotateTransform();
            var spriteTransforms = new TransformGroup();
            spriteTransforms.Children.Add(_spriteScale);
            spriteTransforms.Children.Add(_spriteTilt);

            _sprite2D = Create2DSprite();
            _sprite2D.RenderTransform = spriteTransforms;
            _sprite2D.RenderTransformOrigin = new Point(0.5, 0.5);

            _sprite3D = Create3DSprite(out _modelRotation);

            var root = new Grid { Background = Brushes.Transparent };
            root.Children.Add(_sprite2D);
            root.Children.Add(_sprite3D);
            Content = root;

            ContextMenu = CreatePetContextMenu();
            ContextMenuOpening += (_, _) => RefreshContextMenu();

            PreviewMouseLeftButtonDown += Pet_MouseLeftButtonDown;
            PreviewMouseMove += Pet_MouseMove;
            PreviewMouseLeftButtonUp += Pet_MouseLeftButtonUp;

            Loaded += Pet_Loaded;
            Closed += Pet_Closed;

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += (_, _) => UpdatePet();

            SetDisplayMode(App.Settings.Prop.DesktopPetUse3D);
        }

        private static Canvas Create2DSprite()
        {
            const double center = PetSize / 2;
            var canvas = new Canvas
            {
                Width = PetSize,
                Height = PetSize,
                Background = Brushes.Transparent,
                ClipToBounds = false,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(70, 36, 22),
                    Opacity = 0.24,
                    BlurRadius = 8,
                    ShadowDepth = 3,
                    RenderingBias = RenderingBias.Performance
                }
            };

            Color orange = Color.FromRgb(239, 111, 57);
            Color darkOrange = Color.FromRgb(194, 76, 43);
            Brush orangeBrush = new SolidColorBrush(orange);
            Brush darkBrush = new SolidColorBrush(Color.FromRgb(67, 43, 42));
            Brush creamBrush = new SolidColorBrush(Color.FromRgb(255, 235, 215));
            Brush blushBrush = new SolidColorBrush(Color.FromRgb(255, 174, 145));

            // Eight rounded rays make a soft, friendly sun-shaped Claude creature.
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4 - Math.PI / 2;
                double radius = 33;
                double x = center + Math.Cos(angle) * radius;
                double y = center + Math.Sin(angle) * radius;

                var ray = new Ellipse
                {
                    Width = 20,
                    Height = 29,
                    Fill = orangeBrush,
                    Stroke = new SolidColorBrush(darkOrange),
                    StrokeThickness = 1.2,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(angle * 180 / Math.PI + 90)
                };
                Canvas.SetLeft(ray, x - ray.Width / 2);
                Canvas.SetTop(ray, y - ray.Height / 2);
                canvas.Children.Add(ray);
            }

            var body = new Ellipse
            {
                Width = 61,
                Height = 61,
                Fill = orangeBrush,
                Stroke = new SolidColorBrush(darkOrange),
                StrokeThickness = 1.8
            };
            Canvas.SetLeft(body, center - body.Width / 2);
            Canvas.SetTop(body, center - body.Height / 2 + 1);
            canvas.Children.Add(body);

            var face = new Ellipse
            {
                Width = 43,
                Height = 34,
                Fill = creamBrush
            };
            Canvas.SetLeft(face, center - face.Width / 2);
            Canvas.SetTop(face, center - 11);
            canvas.Children.Add(face);

            AddEllipse(canvas, 42, 46, 6.5, 9, darkBrush);
            AddEllipse(canvas, 58, 46, 6.5, 9, darkBrush);
            AddEllipse(canvas, 43.2, 47.3, 2, 2.5, Brushes.White);
            AddEllipse(canvas, 59.2, 47.3, 2, 2.5, Brushes.White);
            AddEllipse(canvas, 34, 59, 7, 4, blushBrush);
            AddEllipse(canvas, 65, 59, 7, 4, blushBrush);

            var smile = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 48,62 Q 52,68 57,62"),
                Stroke = darkBrush,
                StrokeThickness = 2.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent
            };
            canvas.Children.Add(smile);

            // Tiny feet peek out underneath the round body.
            AddEllipse(canvas, 38, 78, 13, 8, orangeBrush);
            AddEllipse(canvas, 55, 78, 13, 8, orangeBrush);

            return canvas;
        }

        private static void AddEllipse(Canvas canvas, double left, double top, double width, double height, Brush fill)
        {
            var ellipse = new Ellipse { Width = width, Height = height, Fill = fill };
            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);
            canvas.Children.Add(ellipse);
        }

        private static Viewport3D Create3DSprite(out AxisAngleRotation3D rotation)
        {
            var viewport = new Viewport3D
            {
                Width = PetSize,
                Height = PetSize,
                ClipToBounds = false,
                IsHitTestVisible = true,
                Camera = new PerspectiveCamera
                {
                    Position = new Point3D(0, 0.05, 3.25),
                    LookDirection = new Vector3D(0, -0.02, -3.25),
                    UpDirection = new Vector3D(0, 1, 0),
                    FieldOfView = 42
                }
            };

            var model = new Model3DGroup();
            model.Children.Add(new AmbientLight(Color.FromRgb(160, 150, 145)));
            model.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.4, -0.7, -1)));
            model.Children.Add(new DirectionalLight(Color.FromRgb(255, 190, 145), new Vector3D(0.8, 0.25, 0.5)));

            Material orange = CreateMaterial(Color.FromRgb(239, 111, 57));
            Material darkOrange = CreateMaterial(Color.FromRgb(190, 72, 42));
            Material cream = CreateMaterial(Color.FromRgb(255, 232, 209));
            Material dark = CreateMaterial(Color.FromRgb(53, 39, 39));
            Material white = CreateMaterial(Colors.White);

            // Main rounded body and its eight low-poly sun rays.
            model.Children.Add(CreateEllipsoid(new Point3D(0, -0.03, 0), 0.57, 0.58, 0.42, orange));
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                double x = Math.Cos(angle) * 0.56;
                double y = Math.Sin(angle) * 0.56;
                model.Children.Add(CreateEllipsoid(
                    new Point3D(x, y - 0.03, -0.02),
                    0.17,
                    0.22,
                    0.16,
                    darkOrange
                ));
                model.Children.Add(CreateEllipsoid(
                    new Point3D(x * 0.91, y * 0.91 - 0.03, 0.015),
                    0.145,
                    0.19,
                    0.15,
                    orange
                ));
            }

            model.Children.Add(CreateEllipsoid(new Point3D(0, -0.15, 0.39), 0.38, 0.29, 0.13, cream));
            model.Children.Add(CreateEllipsoid(new Point3D(-0.16, 0.09, 0.47), 0.055, 0.075, 0.05, dark));
            model.Children.Add(CreateEllipsoid(new Point3D(0.16, 0.09, 0.47), 0.055, 0.075, 0.05, dark));
            model.Children.Add(CreateEllipsoid(new Point3D(-0.18, 0.12, 0.515), 0.018, 0.022, 0.014, white));
            model.Children.Add(CreateEllipsoid(new Point3D(0.14, 0.12, 0.515), 0.018, 0.022, 0.014, white));
            model.Children.Add(CreateEllipsoid(new Point3D(-0.48, -0.24, 0.08), 0.18, 0.14, 0.15, orange));
            model.Children.Add(CreateEllipsoid(new Point3D(0.48, -0.24, 0.08), 0.18, 0.14, 0.15, orange));
            model.Children.Add(CreateEllipsoid(new Point3D(-0.22, -0.54, 0.06), 0.18, 0.12, 0.16, darkOrange));
            model.Children.Add(CreateEllipsoid(new Point3D(0.22, -0.54, 0.06), 0.18, 0.12, 0.16, darkOrange));

            rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            model.Transform = new RotateTransform3D(rotation);
            viewport.Children.Add(new ModelVisual3D { Content = model });

            return viewport;
        }

        private static Material CreateMaterial(Color color)
        {
            return new DiffuseMaterial(new SolidColorBrush(color));
        }

        private static GeometryModel3D CreateEllipsoid(Point3D center, double radiusX, double radiusY, double radiusZ, Material material)
        {
            const int slices = 18;
            const int stacks = 12;
            var mesh = new MeshGeometry3D();
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            for (int stack = 0; stack <= stacks; stack++)
            {
                double phi = Math.PI * stack / stacks;
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                for (int slice = 0; slice <= slices; slice++)
                {
                    double theta = 2 * Math.PI * slice / slices;
                    double nx = sinPhi * Math.Cos(theta);
                    double ny = cosPhi;
                    double nz = sinPhi * Math.Sin(theta);

                    positions.Add(new Point3D(
                        center.X + nx * radiusX,
                        center.Y + ny * radiusY,
                        center.Z + nz * radiusZ
                    ));
                    normals.Add(new Vector3D(nx, ny, nz));
                }
            }

            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int first = stack * (slices + 1) + slice;
                    int second = first + slices + 1;

                    indices.Add(first);
                    indices.Add(second);
                    indices.Add(first + 1);
                    indices.Add(second);
                    indices.Add(second + 1);
                    indices.Add(first + 1);
                }
            }

            mesh.Positions = positions;
            mesh.Normals = normals;
            mesh.TriangleIndices = indices;

            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };
        }

        private ContextMenu CreatePetContextMenu()
        {
            var menu = new ContextMenu();
            var pauseItem = new MenuItem { Header = "Pause movement", Name = "PauseMovementItem" };
            pauseItem.Click += (_, _) =>
            {
                _paused = !_paused;
                pauseItem.Header = _paused ? "Resume movement" : "Pause movement";
                if (_paused)
                    _motion = PetMotionState.Paused;
                else
                    _motion = FindCurrentPlatform() is null ? PetMotionState.Falling : PetMotionState.Walking;
            };

            var mode2D = new MenuItem { Header = "2D cartoon", Name = "Mode2DItem", IsCheckable = true };
            mode2D.Click += (_, _) => SetDisplayMode(false);

            var mode3D = new MenuItem { Header = "3D toy", Name = "Mode3DItem", IsCheckable = true };
            mode3D.Click += (_, _) => SetDisplayMode(true);

            var closeItem = new MenuItem { Header = "Close Claude pet" };
            closeItem.Click += (_, _) => Close();

            menu.Items.Add(pauseItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(mode2D);
            menu.Items.Add(mode3D);
            menu.Items.Add(new Separator());
            menu.Items.Add(closeItem);
            return menu;
        }

        private void RefreshContextMenu()
        {
            if (ContextMenu is null)
                return;

            foreach (object item in ContextMenu.Items)
            {
                if (item is not MenuItem menuItem)
                    continue;

                if (menuItem.Name == "PauseMovementItem")
                    menuItem.Header = _paused ? "Resume movement" : "Pause movement";
                else if (menuItem.Name == "Mode2DItem")
                    menuItem.IsChecked = !_use3D;
                else if (menuItem.Name == "Mode3DItem")
                    menuItem.IsChecked = _use3D;
            }
        }

        private void Pet_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPlatforms();
            RestorePosition();

            _nextTrick = DateTime.UtcNow.AddSeconds(_random.Next(3, 7));
            _nextWindowRefresh = DateTime.UtcNow.AddSeconds(2);
            _timer.Start();
        }

        private void Pet_Closed(object? sender, EventArgs e)
        {
            _timer.Stop();
        }

        private void RestorePosition()
        {
            double virtualRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

            Left = virtualRight - PetSize - 36;
            Top = virtualBottom - PetSize * 0.91 - 6;

            _currentPlatform = FindCurrentPlatform();
            if (_currentPlatform is not null)
            {
                Top = _currentPlatform.Top;
                _motion = PetMotionState.Walking;
            }
            else
            {
                _motion = PetMotionState.Falling;
                _velocityY = 0;
            }
        }

        private void RefreshPlatforms()
        {
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            var platforms = new List<PetPlatform>
            {
                new PetPlatform(
                    virtualLeft - PetSize,
                    virtualBottom - PetSize * 0.91,
                    virtualRight,
                    true,
                    IntPtr.Zero
                )
            };

            double scaleX = VisualTreeHelper.GetDpi(this).DpiScaleX;
            double scaleY = VisualTreeHelper.GetDpi(this).DpiScaleY;

            EnumWindows((handle, _) =>
            {
                if (handle == new System.Windows.Interop.WindowInteropHelper(this).Handle
                    || !IsWindowVisible(handle)
                    || IsIconic(handle))
                    return true;

                GetWindowThreadProcessId(handle, out uint processId);
                if (processId == (uint)Environment.ProcessId)
                    return true;

                var classNameBuilder = new System.Text.StringBuilder(128);
                GetClassName(handle, classNameBuilder, classNameBuilder.Capacity);
                string className = classNameBuilder.ToString();

                if (className is "Shell_TrayWnd" or "Progman" or "WorkerW" or "TaskListThumbnailWnd")
                    return true;

                if (!GetWindowRect(handle, out NativeRect rect))
                    return true;

                double width = (rect.Right - rect.Left) / scaleX;
                double height = (rect.Bottom - rect.Top) / scaleY;

                if (width < 190 || height < 105)
                    return true;

                double left = rect.Left / scaleX;
                double right = rect.Right / scaleX;
                // Windows touching the top of the monitor still provide a usable
                // ledge; clamp the mascot to the visible desktop rather than
                // excluding those maximized/full-screen windows.
                double top = Math.Max(virtualTop, rect.Top / scaleY - PetSize + 10);

                left = Math.Max(virtualLeft, left - PetSize * 0.35);
                right = Math.Min(virtualRight - PetSize, right - PetSize * 0.65);

                if (right - left > 70 && top >= virtualTop && top < virtualBottom - PetSize)
                    platforms.Add(new PetPlatform(left, top, right, false, handle));

                return true;
            }, IntPtr.Zero);

            _platforms = platforms;

            if (_currentPlatform is not null)
            {
                _currentPlatform = _platforms
                    .OrderBy(x => Math.Abs(x.Top - Top) + DistanceToRange(Left, x.Left, x.Right))
                    .FirstOrDefault();
            }
        }

        private static double DistanceToRange(double value, double minimum, double maximum)
        {
            if (value < minimum)
                return minimum - value;
            if (value > maximum)
                return value - maximum;
            return 0;
        }

        private PetPlatform? FindCurrentPlatform()
        {
            return _platforms
                .Where(x => Math.Abs((Top) - x.Top) < 30 && Left + PetSize * 0.7 >= x.Left && Left + PetSize * 0.3 <= x.Right)
                .OrderBy(x => Math.Abs(Top - x.Top))
                .FirstOrDefault();
        }

        private void UpdatePet()
        {
            DateTime now = DateTime.UtcNow;
            double delta = Math.Clamp((now - _lastTick).TotalSeconds, 0.001, 0.05);
            _lastTick = now;

            if (now >= _nextWindowRefresh)
            {
                RefreshPlatforms();
                _nextWindowRefresh = now.AddSeconds(2);
            }

            if (!_dragging && !_paused)
            {
                switch (_motion)
                {
                    case PetMotionState.Walking:
                        UpdateWalking(delta, now);
                        break;
                    case PetMotionState.Jumping:
                    case PetMotionState.Falling:
                    case PetMotionState.Thrown:
                        UpdateFlight(delta);
                        break;
                    case PetMotionState.Climbing:
                        UpdateClimbing(delta);
                        break;
                }
            }

            AnimatePet(now);
        }

        private void UpdateWalking(double delta, DateTime now)
        {
            if (_currentPlatform is null)
                _currentPlatform = FindCurrentPlatform();

            if (_currentPlatform is null)
            {
                _motion = PetMotionState.Falling;
                return;
            }

            Top = _currentPlatform.Top;
            Left += _direction * WalkSpeed * delta;

            if (Left <= _currentPlatform.Left)
            {
                Left = _currentPlatform.Left;
                _direction = 1;
            }
            else if (Left >= _currentPlatform.Right)
            {
                Left = _currentPlatform.Right;
                _direction = -1;
            }

            if (now < _nextTrick || !App.Settings.Prop.DesktopPetJumpOnWindows)
                return;

            _nextTrick = now.AddSeconds(_random.Next(4, 9));

            double petCenter = Left + PetSize / 2;
            PetPlatform? target = _platforms
                .Where(x => x != _currentPlatform
                    && !x.IsScreen
                    && x.Top < _currentPlatform.Top - 26
                    && DistanceToRange(petCenter, x.Left, x.Right) < 360)
                .OrderBy(x => Math.Abs(x.Top - _currentPlatform.Top) + DistanceToRange(petCenter, x.Left, x.Right) * 0.8)
                .FirstOrDefault();

            if (target is not null)
            {
                if (Math.Abs(target.Top - Top) < 175)
                    StartJump(target);
                else
                    StartClimb(target);
            }
            else if (_currentPlatform.IsScreen)
            {
                StartHop();
            }
        }

        private void StartJump(PetPlatform target)
        {
            _targetPlatform = target;
            double targetLeft = Math.Clamp(Left + _direction * 34, target.Left, target.Right);
            double deltaX = targetLeft - Left;
            double deltaY = target.Top - Top;
            double flightTime = Math.Clamp(0.58 + Math.Abs(deltaX) / 520, 0.58, 1.05);

            _velocityX = deltaX / flightTime;
            _velocityY = (deltaY - 0.5 * Gravity * flightTime * flightTime) / flightTime;
            _motion = PetMotionState.Jumping;
        }

        private void StartHop()
        {
            _targetPlatform = _currentPlatform;
            _velocityX = _direction * 34;
            _velocityY = -345;
            _motion = PetMotionState.Jumping;
        }

        private void StartClimb(PetPlatform target)
        {
            _targetPlatform = target;

            // Pick the closest side of the target window, climb up its frame,
            // then step onto the upper edge.
            double leftSide = target.Left - PetSize * 0.78;
            double rightSide = target.Right - PetSize * 0.22;
            _climbSideX = Math.Abs(Left - leftSide) <= Math.Abs(Left - rightSide)
                ? leftSide
                : rightSide;

            _direction = _climbSideX < Left ? -1 : 1;
            _velocityY = -145;
            _motion = PetMotionState.Climbing;
        }

        private void UpdateClimbing(double delta)
        {
            if (_targetPlatform is null)
            {
                _motion = PetMotionState.Falling;
                return;
            }

            Left += (_climbSideX - Left) * Math.Min(1, delta * 5);
            Top += _velocityY * delta;

            if (Top <= _targetPlatform.Top)
            {
                Top = _targetPlatform.Top;
                Left = Math.Clamp(_climbSideX, _targetPlatform.Left, _targetPlatform.Right);
                _currentPlatform = _targetPlatform;
                _targetPlatform = null;
                _motion = PetMotionState.Walking;
            }
        }

        private void UpdateFlight(double delta)
        {
            double oldBottom = Top + PetSize * 0.91;

            Left += _velocityX * delta;
            Top += _velocityY * delta;
            _velocityY += Gravity * delta;

            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            if (Left < virtualLeft)
            {
                Left = virtualLeft;
                _velocityX = Math.Abs(_velocityX) * 0.48;
                _direction = 1;
            }
            else if (Left + PetSize > virtualRight)
            {
                Left = virtualRight - PetSize;
                _velocityX = -Math.Abs(_velocityX) * 0.48;
                _direction = -1;
            }

            if (Top < virtualTop)
            {
                Top = virtualTop;
                _velocityY = Math.Abs(_velocityY) * 0.45;
            }

            double newBottom = Top + PetSize * 0.91;
            if (_velocityY > 0)
            {
                PetPlatform? landing = _platforms
                    .Where(x => oldBottom <= x.Top + 3
                        && newBottom >= x.Top
                        && Left + PetSize * 0.78 >= x.Left
                        && Left + PetSize * 0.22 <= x.Right)
                    .OrderBy(x => Math.Abs(Top - x.Top))
                    .FirstOrDefault();

                if (landing is not null)
                {
                    Top = landing.Top;
                    _currentPlatform = landing;
                    _targetPlatform = null;
                    _velocityY = 0;
                    _velocityX = 0;
                    _motion = PetMotionState.Walking;
                    _nextTrick = DateTime.UtcNow.AddSeconds(_random.Next(3, 8));
                    return;
                }
            }

            if (Top > virtualBottom - PetSize)
            {
                Top = virtualBottom - PetSize;
                _currentPlatform = _platforms.FirstOrDefault(x => x.IsScreen);
                _velocityY = 0;
                _velocityX = 0;
                _motion = PetMotionState.Walking;
            }
        }

        private void AnimatePet(DateTime now)
        {
            double phase = now.TimeOfDay.TotalSeconds * (_motion == PetMotionState.Walking ? 13 : 5);
            double bob = Math.Sin(phase) * (_motion == PetMotionState.Walking ? 2.8 : 1.2);

            _spriteScale.ScaleX = _direction;
            _spriteScale.ScaleY = 1 + Math.Abs(Math.Sin(phase)) * 0.035;
            _spriteTilt.Angle = _motion switch
            {
                PetMotionState.Climbing => -_direction * 9,
                PetMotionState.Jumping => _direction * -8,
                PetMotionState.Thrown => Math.Clamp(_velocityX / 36, -24, 24),
                PetMotionState.Dragging => Math.Clamp(_velocityX / 55, -18, 18),
                _ => Math.Sin(phase) * 4
            };

            _sprite2D.Margin = new Thickness(0, bob, 0, 0);
            _modelRotation.Angle = _motion == PetMotionState.Walking
                ? Math.Sin(phase) * 9
                : _motion == PetMotionState.Climbing ? -12 : Math.Clamp(_velocityX / 28, -32, 32);
        }

        private void SetDisplayMode(bool use3D)
        {
            _use3D = use3D;
            _sprite2D.Visibility = use3D ? Visibility.Collapsed : Visibility.Visible;
            _sprite3D.Visibility = use3D ? Visibility.Visible : Visibility.Collapsed;
        }

        private Point GetPointerPositionInDips(Point windowPoint)
        {
            // PointToScreen returns device coordinates. Window.Left/Top and the
            // drag offset are WPF DIPs, so convert before moving the layered window.
            Point screenPoint = PointToScreen(windowPoint);
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            return new Point(screenPoint.X / dpi.DpiScaleX, screenPoint.Y / dpi.DpiScaleY);
        }

        private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            _dragging = true;
            _motion = PetMotionState.Dragging;
            _velocityX = 0;
            _velocityY = 0;
            _dragOffset = e.GetPosition(this);
            _lastPointer = GetPointerPositionInDips(e.GetPosition(this));
            _lastPointerTime = DateTime.UtcNow;
            CaptureMouse();
            e.Handled = true;
        }

        private void Pet_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point pointer = GetPointerPositionInDips(e.GetPosition(this));
            DateTime now = DateTime.UtcNow;
            double elapsed = Math.Max(0.001, (now - _lastPointerTime).TotalSeconds);

            double newVelocityX = (pointer.X - _lastPointer.X) / elapsed;
            double newVelocityY = (pointer.Y - _lastPointer.Y) / elapsed;
            _velocityX = Math.Clamp(_velocityX * 0.35 + newVelocityX * 0.65, -1200, 1200);
            _velocityY = Math.Clamp(_velocityY * 0.35 + newVelocityY * 0.65, -1200, 1200);

            Left = pointer.X - _dragOffset.X;
            Top = pointer.Y - _dragOffset.Y;

            _lastPointer = pointer;
            _lastPointerTime = now;
            e.Handled = true;
        }

        private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging || e.ChangedButton != MouseButton.Left)
                return;

            _dragging = false;
            ReleaseMouseCapture();

            double throwSpeed = Math.Sqrt(_velocityX * _velocityX + _velocityY * _velocityY);
            if (throwSpeed < 130)
            {
                _currentPlatform = FindCurrentPlatform();
                _motion = _currentPlatform is null ? PetMotionState.Falling : PetMotionState.Walking;
                _velocityX = 0;
                _velocityY = 0;
            }
            else
            {
                _velocityX *= 1.25;
                _velocityY *= 1.25;
                _motion = PetMotionState.Thrown;
            }

            e.Handled = true;
        }

        private delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private sealed class PetPlatform(double left, double top, double right, bool isScreen, IntPtr handle)
        {
            public double Left { get; } = left;
            public double Top { get; } = top;
            public double Right { get; } = right;
            public bool IsScreen { get; } = isScreen;
            public IntPtr Handle { get; } = handle;
        }

        private enum PetMotionState
        {
            Walking,
            Jumping,
            Falling,
            Climbing,
            Dragging,
            Thrown,
            Paused
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder className, int maxCount);
    }
}

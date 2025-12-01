using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using System.Reflection; // for SetDoubleBuffered
using System.Drawing.Drawing2D;

namespace MouseSliderApp
{
    public class Form1 : Form
    {
        // ====== Pages ======
        private Panel _pageProfiles = null!;
        private Panel _pageSettings = null!;
        private Panel _pageAbout = null!;
        private Panel _pageTutorial = null!;

        // Container + custom scrollbar for profiles
        private Panel _profilesContainer = null!;
        private Panel _profilesScrollTrack = null!;   // fake scrollbar track
        private Panel _profilesScrollThumb = null!;   // fake scrollbar thumb
        private int _profilesScrollOffset = 0;
        private int _profilesMaxScrollOffset = 0;
        private const int ProfilesScrollStep = 40;

        // ====== Movement controls (Settings page) ======
        private Label _labelHorizontal = null!;
        private TrackBar _trackBarHorizontal = null!;
        private TextBox _textHorizontal = null!;

        private Label _labelVertical = null!;
        private TrackBar _trackBarVertical = null!;
        private TextBox _textVertical = null!;

        // Global buttons on profiles page
        private Button _buttonStart = null!;
        private Button _buttonResetAll = null!;
        private Button _buttonAbout = null!;
        private Button _buttonTutorial = null!;

        // movement + setup "cards"
        private Panel _movementCard = null!;
        private Panel _setupCard = null!;

        // ====== Category / Profile selection (Profiles page) ======
        private Label _labelCategory = null!;
        private Button _buttonCategoryA = null!;
        private Button _buttonCategoryB = null!;
        private TextBox _searchBox = null!;
        private Label _labelSelectedProfile = null!;
        private Label _labelSelectedSetup = null!;
        private FlowLayoutPanel _profilesPanel = null!;
        private Panel? _selectedProfileCard;
        private Panel _profilesTopBar = null!;

        // search placeholder state
        private bool _searchHasPlaceholder = true;
        private const string SearchPlaceholder = "Search...";

        // ====== Setups + Keybinds (Settings page) ======
        private Label _labelActiveSetup = null!;
        private Label _labelSetup1 = null!;
        private TextBox _textKey1 = null!;
        private Button _buttonSetKey1 = null!;
        private Button _buttonSaveSetup1 = null!;

        private Label _labelSetup2 = null!;
        private TextBox _textKey2 = null!;
        private Button _buttonSetKey2 = null!;
        private Button _buttonSaveSetup2 = null!;

        // ====== Profile image (Settings page) ======
        private PictureBox _pictureProfile = null!;
        private Label _labelEditingProfile = null!;
        private Button _buttonBack = null!;
        private Button _buttonResetProfile = null!;   // per-profile reset

        // Settings watermark logo
        private PictureBox _settingsLogoWatermark = null!;

        // ====== Movement state ======
        private bool _isActive;
        private bool _comboArmed;
        private bool _comboActive;

        private System.Windows.Forms.Timer _movementTimer = null!;

        private double _horizontalSpeed;
        private double _verticalSpeed;
        private double _accumulatedX;
        private double _accumulatedY;

        private const double SliderScale = 100.0;

        // Card colors (for hover/selection)
        private static readonly Color CardNormalColor = Color.FromArgb(30, 41, 59);
        private static readonly Color CardHoverColor = Color.FromArgb(51, 65, 85);
        private static readonly Color CardSelectedColor = Color.FromArgb(37, 99, 235);

        // Theme colors
        private static readonly Color BgMain = Color.FromArgb(10, 12, 24);
        private static readonly Color BgHeader = Color.FromArgb(15, 23, 42);
        private static readonly Color BgTopBar = Color.FromArgb(17, 24, 39);
        private static readonly Color BgSettings = Color.FromArgb(15, 23, 42);

        private static readonly Color AccentPrimary = Color.FromArgb(59, 130, 246);   // primary blue
        private static readonly Color AccentPrimarySoft = Color.FromArgb(37, 99, 235);
        private static readonly Color AccentPositive = Color.FromArgb(34, 197, 94);   // green
        private static readonly Color AccentDanger = Color.FromArgb(239, 68, 68);     // red
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

        private const string ActiveBadgeName = "ActiveBadge";

        // App logo (shared) – used in headers + watermark
        private Image? _appLogoImage;

        // ====== Profile model ======
        private class Profile
        {
            public string Category { get; }
            public int Index { get; }
            public string Name { get; }
            public string? ImageFileName { get; }

            public double Horizontal1 { get; set; }
            public double Vertical1 { get; set; }
            public Keys Key1 { get; set; } = Keys.None;

            public double Horizontal2 { get; set; }
            public double Vertical2 { get; set; }
            public Keys Key2 { get; set; } = Keys.None;

            public Profile(string category, int index, string name, string? imageFileName)
            {
                Category = category;
                Index = index;
                Name = name;
                ImageFileName = imageFileName;
                Horizontal1 = 0.0;
                Vertical1 = 0.0;
                Horizontal2 = 0.0;
                Vertical2 = 0.0;
            }

            public override string ToString() => Name;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryEnableDarkTitleBar();
        }

        private class ProfileData
        {
            public string Category { get; set; } = "";
            public int Index { get; set; }
            public double Horizontal1 { get; set; }
            public double Vertical1 { get; set; }
            public double Horizontal2 { get; set; }
            public double Vertical2 { get; set; }
            public Keys Key1 { get; set; }
            public Keys Key2 { get; set; }
        }

        private readonly List<Profile> _profiles = new();
        private readonly Dictionary<Profile, Panel> _profileCardCache = new(); // cache cards

        private string _currentCategory = "A";
        private string _currentSearchText = string.Empty;
        private Profile? _currentProfile;

        private int _currentSetupIndex = 1;
        private int _capturingKeyForSetup = 0;
        private bool _key1WasDown;
        private bool _key2WasDown;

        private readonly string _dataFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles.json");
        private readonly string _imagesFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        public Form1()
        {
            KeyPreview = true;
            InitializeUi();

            // High-res icon for the window (Windows will still draw it small in the title bar)
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch
            {
                // ignore if something goes wrong
            }

            // existing stuff
            SetDoubleBuffered(this);
            SetDoubleBuffered(_pageProfiles);
            SetDoubleBuffered(_pageSettings);
            SetDoubleBuffered(_profilesPanel);

            CreateProfiles();
            LoadProfilesFromFile();
            ShowCategory("A");
            ShowProfilesPage();
        }

        // For dark title bar / border
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void TryEnableDarkTitleBar()
        {
            try
            {
                int useDark = 1;
                // Ask Windows to use the dark title bar / border for this window
                DwmSetWindowAttribute(
                    this.Handle,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref useDark,
                    sizeof(int));
            }
            catch
            {
                // If it fails (older Windows), just ignore – app still works
            }
        }

        // ==========================================================
        // UI setup
        // ==========================================================
        private void InitializeUi()
        {
            Text = "Mouse Slider Controller";
            ClientSize = new Size(1100, 650);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = BgMain;
            ForeColor = Color.White;

            KeyDown += Form1_KeyDown;
            FormClosing += Form1_FormClosing;

            _movementTimer = new System.Windows.Forms.Timer { Interval = 20 };
            _movementTimer.Tick += MovementTimer_Tick;
            _movementTimer.Start();

            _pageProfiles = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain
            };

            _pageSettings = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain,
                Visible = false
            };

            _pageAbout = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain,
                Visible = false
            };

            _pageTutorial = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain,
                Visible = false
            };

            Controls.Add(_pageTutorial);
            Controls.Add(_pageAbout);
            Controls.Add(_pageSettings);
            Controls.Add(_pageProfiles);

            // load logo once
            LoadAppLogo();

            BuildProfilesPage();
            BuildSettingsPage();
            BuildAboutPage();
            BuildTutorialPage();
        }

        private void LoadAppLogo()
        {
            // Put your logo at /Images/AppLogo.png
            try
            {
                string logoPath = Path.Combine(_imagesFolder, "AppLogo.png");
                if (!File.Exists(logoPath))
                    return;

                using (var img = Image.FromFile(logoPath))
                {
                    _appLogoImage = new Bitmap(img);
                }
            }
            catch
            {
                // ignore – stays null
            }
        }

        // ===== Profiles page (main menu) =====
        private void BuildProfilesPage()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = BgHeader
            };

            // BIGGER logo in header
            var logoBox = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(15, 10),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            if (_appLogoImage != null)
            {
                logoBox.Image = _appLogoImage;
            }
            else
            {
                logoBox.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(55, 65, 81), 2))
                    {
                        e.Graphics.DrawRectangle(pen, 3, 3, logoBox.Width - 6, logoBox.Height - 6);
                    }
                };
            }

            var titleLabel = new Label
            {
                AutoSize = true,
                Text = "Precision Mouse Profiles",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(logoBox.Right + 12, 14)
            };

            _buttonResetAll = new Button
            {
                Text = "RESET ALL",
                Width = 120,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentDanger,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top
            };
            _buttonResetAll.FlatAppearance.BorderSize = 0;
            _buttonResetAll.Click += ButtonResetAll_Click;
            ApplyRoundedCorners(_buttonResetAll, 6);

            _buttonStart = new Button
            {
                Text = "Start",
                Width = 90,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentPrimary,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top
            };
            _buttonStart.FlatAppearance.BorderSize = 0;
            _buttonStart.Click += ButtonStart_Click;
            ApplyRoundedCorners(_buttonStart, 6);

            _buttonAbout = CreateFlatButton("About", 80, 30);
            _buttonTutorial = CreateFlatButton("Tutorial", 80, 30);

            _buttonAbout.Click += (s, e) => ShowAboutPage();
            _buttonTutorial.Click += (s, e) => ShowTutorialPage();

            header.Controls.Add(logoBox);
            header.Controls.Add(titleLabel);
            header.Controls.Add(_buttonResetAll);
            header.Controls.Add(_buttonStart);
            header.Controls.Add(_buttonAbout);
            header.Controls.Add(_buttonTutorial);

            // === UPDATED LAYOUT: Start + Reset centered, About + Tutorial on right ===
            header.Resize += (s, e) =>
            {
                int top = 15;
                int marginRight = 20;
                int gap = 10;

                // Right side: Tutorial then About
                int xRight = header.Width - marginRight;

                if (_buttonTutorial != null)
                {
                    _buttonTutorial.Location = new Point(
                        xRight - _buttonTutorial.Width,
                        top);
                    xRight = _buttonTutorial.Left - gap;
                }

                if (_buttonAbout != null)
                {
                    _buttonAbout.Location = new Point(
                        xRight - _buttonAbout.Width,
                        top);
                }

                // Center group: Start + RESET ALL
                if (_buttonStart != null && _buttonResetAll != null)
                {
                    int groupWidth = _buttonStart.Width + gap + _buttonResetAll.Width;
                    int centerX = header.Width / 2;
                    int groupLeft = centerX - groupWidth / 2;
                    if (groupLeft < 0) groupLeft = 0;

                    _buttonStart.Location = new Point(groupLeft, top);
                    _buttonResetAll.Location = new Point(_buttonStart.Right + gap, top);
                }
            };

            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(31, 41, 55)))
                {
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
                }
            };

            // top bar with category + selected label + search
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = BgTopBar,
                Padding = new Padding(15, 10, 15, 5)
            };
            _profilesTopBar = topBar;

            topBar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(31, 41, 55)))
                {
                    e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
                }
            };



            _buttonCategoryA = CreateFlatButton("Attackers", 100, 28);
            _buttonCategoryB = CreateFlatButton("Defenders", 100, 28);

            StyleSegmentButton(_buttonCategoryA, true);
            StyleSegmentButton(_buttonCategoryB, false);

            _buttonCategoryA.Click += (s, e) => ShowCategory("A");
            _buttonCategoryB.Click += (s, e) => ShowCategory("B");

            // search box with placeholder
            _searchBox = new TextBox
            {
                Width = 170,
                Height = 24,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = TextMuted,
                Text = SearchPlaceholder
            };
            _searchHasPlaceholder = true;
            _searchBox.GotFocus += SearchBox_GotFocus;
            _searchBox.LostFocus += SearchBox_LostFocus;
            _searchBox.TextChanged += SearchBox_TextChanged;

            _labelSelectedProfile = new Label
            {
                AutoSize = true,
                Text = "Selected profile: (none)",
                ForeColor = TextMuted
            };

            _labelSelectedSetup = new Label
            {
                AutoSize = true,
                Text = "Setup: (none)",
                ForeColor = TextMuted
            };

            topBar.Controls.Add(_buttonCategoryA);
            topBar.Controls.Add(_buttonCategoryB);
            topBar.Controls.Add(_searchBox);
            topBar.Controls.Add(_labelSelectedProfile);
            topBar.Controls.Add(_labelSelectedSetup);

            topBar.Resize += (s, e) => LayoutProfilesTopBar();
            LayoutProfilesTopBar();

            // profile cards container + FAKE custom scrollbar
            _profilesContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain
            };

            _profilesScrollTrack = new Panel
            {
                Dock = DockStyle.Right,
                Width = 6,
                BackColor = BgHeader
            };

            _profilesScrollThumb = new Panel
            {
                Width = _profilesScrollTrack.Width,
                Height = 40,
                BackColor = AccentPrimarySoft,
                Visible = false
            };
            ApplyRoundedCorners(_profilesScrollThumb, 3);
            _profilesScrollTrack.Controls.Add(_profilesScrollThumb);

            _profilesPanel = new FlowLayoutPanel
            {
                AutoScroll = false,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BgMain,
                Location = new Point(0, 0),
                Padding = new Padding(0, 20, 0, 20)
            };
            _profilesPanel.Resize += ProfilesPanel_Resize;
            _profilesPanel.ControlAdded += ProfilesPanel_ControlChanged;
            _profilesPanel.ControlRemoved += ProfilesPanel_ControlChanged;
            _profilesPanel.MouseWheel += ProfilesPanel_MouseWheel;

            _profilesContainer.Controls.Add(_profilesPanel);
            _profilesContainer.Controls.Add(_profilesScrollTrack);
            _profilesContainer.Resize += ProfilesContainer_Resize;

            _pageProfiles.Controls.Add(_profilesContainer);
            _pageProfiles.Controls.Add(topBar);
            _pageProfiles.Controls.Add(header);
        }

        // Lay out the top bar whenever size or text changes
        private void LayoutProfilesTopBar()
        {
            if (_profilesTopBar == null)
                return;

            int gap = 10;
            int y = 10;
            int rightMargin = 20;

            // left: search box
            if (_searchBox != null)
            {
                _searchBox.Location = new Point(15, y + 3);
            }

            // center: Attackers / Defenders ONLY (no "Side" text)
            if (_buttonCategoryA == null || _buttonCategoryB == null)
                return;

            int totalWidth = _buttonCategoryA.Width + gap + _buttonCategoryB.Width;
            int startX = (_profilesTopBar.ClientSize.Width - totalWidth) / 2;
            if (startX < 10) startX = 10;

            _buttonCategoryA.Location = new Point(startX, y);
            _buttonCategoryB.Location = new Point(_buttonCategoryA.Right + gap, y);

            // right: selected profile + setup
            if (_labelSelectedProfile == null || _labelSelectedSetup == null)
                return;

            int rightX = _profilesTopBar.ClientSize.Width - rightMargin;

            _labelSelectedProfile.Location = new Point(
                rightX - _labelSelectedProfile.Width,
                y + 0);

            _labelSelectedSetup.Location = new Point(
                rightX - _labelSelectedSetup.Width,
                _labelSelectedProfile.Bottom + 2);
        }


        // center profiles + sync custom scrollbar
        private void ProfilesPanel_Resize(object? sender, EventArgs e)
        {
            if (_profilesContainer != null && _profilesScrollTrack != null)
            {
                _profilesPanel.Width = _profilesContainer.ClientSize.Width - _profilesScrollTrack.Width;
            }

            CenterProfiles();
            UpdateProfilesScrollBar();
        }

        private void ProfilesContainer_Resize(object? sender, EventArgs e)
        {
            if (_profilesPanel != null && _profilesScrollTrack != null)
            {
                _profilesPanel.Width = _profilesContainer.ClientSize.Width - _profilesScrollTrack.Width;
            }
            CenterProfiles();
            UpdateProfilesScrollBar();
        }

        private void ProfilesPanel_ControlChanged(object? sender, ControlEventArgs e)
        {
            CenterProfiles();
            UpdateProfilesScrollBar();
        }

        private void CenterProfiles()
        {
            if (_profilesPanel.Controls.Count == 0)
            {
                _profilesPanel.Padding = new Padding(0, 20, 0, 20);
                return;
            }

            int panelWidth = _profilesPanel.ClientSize.Width;
            if (panelWidth <= 0) return;

            var first = _profilesPanel.Controls[0];
            int itemWidth = first.Width + first.Margin.Horizontal;
            if (itemWidth <= 0) return;

            int columns = panelWidth / itemWidth;
            if (columns < 1) columns = 1;

            int usedWidth = columns * itemWidth;
            int extra = panelWidth - usedWidth;
            if (extra < 0) extra = 0;

            int leftPad = extra / 2;
            _profilesPanel.Padding = new Padding(leftPad, 20, 0, 20);
        }

        private void UpdateProfilesScrollBar()
        {
            if (_profilesContainer == null || _profilesPanel == null)
                return;

            _profilesPanel.PerformLayout();

            int contentHeight = 0;
            foreach (Control c in _profilesPanel.Controls)
            {
                if (c.Bottom > contentHeight)
                    contentHeight = c.Bottom;
            }

            int viewportHeight = _profilesContainer.ClientSize.Height;
            if (viewportHeight <= 0) viewportHeight = 1;

            int totalContentHeight = contentHeight + _profilesPanel.Padding.Vertical;
            _profilesPanel.Height = Math.Max(_profilesContainer.ClientSize.Height, totalContentHeight);

            int maxOffset = Math.Max(0, totalContentHeight - viewportHeight);
            _profilesMaxScrollOffset = maxOffset;

            if (_profilesScrollOffset > maxOffset)
                _profilesScrollOffset = maxOffset;
            if (_profilesScrollOffset < 0)
                _profilesScrollOffset = 0;

            bool scrollable = maxOffset > 0;

            if (_profilesScrollTrack != null && _profilesScrollThumb != null)
            {
                if (!scrollable)
                {
                    _profilesScrollThumb.Visible = false;
                }
                else
                {
                    _profilesScrollThumb.Visible = true;

                    int trackHeight = _profilesScrollTrack.ClientSize.Height;
                    if (trackHeight <= 0) trackHeight = 1;

                    double viewportRatio = viewportHeight / (double)totalContentHeight;
                    if (viewportRatio > 1.0) viewportRatio = 1.0;
                    if (viewportRatio < 0.1) viewportRatio = 0.1;

                    int thumbHeight = Math.Max(20, (int)(trackHeight * viewportRatio));
                    if (thumbHeight > trackHeight) thumbHeight = trackHeight;
                    _profilesScrollThumb.Height = thumbHeight;

                    double scrollRatio = maxOffset == 0 ? 0.0 : _profilesScrollOffset / (double)maxOffset;
                    int thumbMaxTravel = trackHeight - thumbHeight;
                    if (thumbMaxTravel < 0) thumbMaxTravel = 0;

                    int thumbTop = (int)(thumbMaxTravel * scrollRatio);
                    if (thumbTop < 0) thumbTop = 0;
                    if (thumbTop > thumbMaxTravel) thumbTop = thumbMaxTravel;

                    _profilesScrollThumb.Top = thumbTop;
                }
            }

            _profilesPanel.Location = new Point(0, -_profilesScrollOffset);
        }

        private void ProfilesPanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_profilesMaxScrollOffset <= 0) return;

            int delta = -e.Delta;
            int step = ProfilesScrollStep;
            int newOffset = _profilesScrollOffset + (delta > 0 ? step : -step);

            if (newOffset < 0)
                newOffset = 0;
            if (newOffset > _profilesMaxScrollOffset)
                newOffset = _profilesMaxScrollOffset;

            _profilesScrollOffset = newOffset;
            UpdateProfilesScrollBar();
        }

        // ===== Settings page =====
        private void BuildSettingsPage()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = BgHeader
            };

            _buttonBack = new Button
            {
                Text = "← Back",
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                Location = new Point(15, 15)
            };
            _buttonBack.FlatAppearance.BorderSize = 0;
            _buttonBack.Click += (s, e) => ShowProfilesPage();
            ApplyRoundedCorners(_buttonBack, 6);

            // BIGGER logo in settings header
            var logoBox = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point(_buttonBack.Right + 15, 14),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            if (_appLogoImage != null)
            {
                logoBox.Image = _appLogoImage;
            }
            else
            {
                logoBox.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(55, 65, 81), 2))
                    {
                        e.Graphics.DrawRectangle(pen, 3, 3, logoBox.Width - 6, logoBox.Height - 6);
                    }
                };
            }

            _labelEditingProfile = new Label
            {
                AutoSize = true,
                Text = "Editing: (none)",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(logoBox.Right + 10, 19)
            };

            _buttonResetProfile = new Button
            {
                Text = "Reset profile",
                Width = 110,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentDanger,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _buttonResetProfile.FlatAppearance.BorderSize = 0;
            ApplyRoundedCorners(_buttonResetProfile, 6);
            _buttonResetProfile.Click += ButtonResetProfile_Click;

            header.Controls.Add(_buttonBack);
            header.Controls.Add(logoBox);
            header.Controls.Add(_labelEditingProfile);
            header.Controls.Add(_buttonResetProfile);

            header.Resize += (s, e) =>
            {
                _buttonResetProfile.Location = new Point(
                    header.Width - _buttonResetProfile.Width - 20,
                    15);
            };

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSettings,
                Padding = new Padding(0)
            };

            var settingsLayout = new Panel
            {
                BackColor = Color.Transparent
            };

            // hero frame + big image
            var operatorFrame = new Panel
            {
                Size = new Size(280, 300),
                BackColor = CardNormalColor,
                Padding = new Padding(12)
            };
            ApplyRoundedCorners(operatorFrame, 12);
            operatorFrame.Location = new Point(0, 0);

            _pictureProfile = new PictureBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 23, 42)
            };
            operatorFrame.Controls.Add(_pictureProfile);

            var operatorCaption = new Label
            {
                AutoSize = true,
                Text = "Current operator",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular)
            };
            operatorCaption.Location = new Point(operatorFrame.Left + 4, operatorFrame.Bottom + 6);

            // movement card
            _movementCard = new Panel
            {
                BackColor = CardNormalColor,
                Size = new Size(460, 200),
                Padding = new Padding(10)
            };
            ApplyRoundedCorners(_movementCard, 8);
            _movementCard.Location = new Point(operatorFrame.Right + 40, operatorFrame.Top);

            var movementTitle = new Label
            {
                AutoSize = true,
                Text = "Movement",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 5)
            };

            // thin accent line under movement title
            var movementUnderline = new Panel
            {
                Height = 2,
                Width = _movementCard.Width - 20,
                BackColor = AccentPrimarySoft,
                Location = new Point(10, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _labelHorizontal = new Label
            {
                AutoSize = true,
                Text = "Horizontal speed",
                Location = new Point(10, 35)
            };

            _trackBarHorizontal = new TrackBar
            {
                Minimum = -10000,
                Maximum = 10000,
                TickFrequency = 2000,
                Value = 0,
                Width = 310,
                Location = new Point(10, 55),
                BackColor = _movementCard.BackColor
            };
            _trackBarHorizontal.Scroll += TrackBarHorizontal_Scroll;

            _textHorizontal = new TextBox
            {
                Width = 80,
                Location = new Point(330, 55),
                Text = "0.000"
            };
            _textHorizontal.KeyDown += TextHorizontal_KeyDown;
            _textHorizontal.Leave += TextHorizontal_Leave;

            _labelVertical = new Label
            {
                AutoSize = true,
                Text = "Vertical speed",
                Location = new Point(10, 95)
            };

            _trackBarVertical = new TrackBar
            {
                Minimum = 0,
                Maximum = 10000,
                TickFrequency = 2000,
                Value = 0,
                Width = 310,
                Location = new Point(10, 115),
                BackColor = _movementCard.BackColor
            };
            _trackBarVertical.Scroll += TrackBarVertical_Scroll;

            _textVertical = new TextBox
            {
                Width = 80,
                Location = new Point(330, 115),
                Text = "0.000"
            };
            _textVertical.KeyDown += TextVertical_KeyDown;
            _textVertical.Leave += TextVertical_Leave;

            _movementCard.Controls.Add(movementTitle);
            _movementCard.Controls.Add(movementUnderline);
            _movementCard.Controls.Add(_labelHorizontal);
            _movementCard.Controls.Add(_trackBarHorizontal);
            _movementCard.Controls.Add(_textHorizontal);
            _movementCard.Controls.Add(_labelVertical);
            _movementCard.Controls.Add(_trackBarVertical);
            _movementCard.Controls.Add(_textVertical);

            // setups card
            _setupCard = new Panel
            {
                BackColor = CardNormalColor,
                Size = new Size(460, 220),
                Padding = new Padding(10)
            };
            ApplyRoundedCorners(_setupCard, 8);
            _setupCard.Location = new Point(_movementCard.Left, _movementCard.Bottom + 20);

            var setupTitle = new Label
            {
                AutoSize = true,
                Text = "Setups & Keybinds",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 5)
            };

            // thin accent line under setups title
            var setupUnderline = new Panel
            {
                Height = 2,
                Width = _setupCard.Width - 20,
                BackColor = AccentPrimarySoft,
                Location = new Point(10, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _labelActiveSetup = new Label
            {
                AutoSize = true,
                Text = "Active setup: 1",
                Location = new Point(10, 30),
                ForeColor = TextMuted
            };

            _labelSetup1 = new Label
            {
                AutoSize = true,
                Text = "Setup 1 key:",
                Location = new Point(10, 60)
            };

            _textKey1 = new TextBox
            {
                ReadOnly = true,
                Width = 80,
                Location = new Point(90, 57),
                Text = "None"
            };

            _buttonSetKey1 = CreateFlatButton("Set Key 1", 90, 28);
            _buttonSetKey1.Location = new Point(180, 55);
            _buttonSetKey1.Click += (s, e) => StartCapturingKey(1);

            _buttonSaveSetup1 = CreateFlatButton("Save 1", 80, 28);
            _buttonSaveSetup1.Location = new Point(280, 55);
            _buttonSaveSetup1.Click += ButtonSaveSetup1_Click;

            _labelSetup2 = new Label
            {
                AutoSize = true,
                Text = "Setup 2 key:",
                Location = new Point(10, 100)
            };

            _textKey2 = new TextBox
            {
                ReadOnly = true,
                Width = 80,
                Location = new Point(90, 97),
                Text = "None"
            };

            _buttonSetKey2 = CreateFlatButton("Set Key 2", 90, 28);
            _buttonSetKey2.Location = new Point(180, 95);
            _buttonSetKey2.Click += (s, e) => StartCapturingKey(2);

            _buttonSaveSetup2 = CreateFlatButton("Save 2", 80, 28);
            _buttonSaveSetup2.Location = new Point(280, 95);
            _buttonSaveSetup2.Click += ButtonSaveSetup2_Click;

            _setupCard.Controls.Add(setupTitle);
            _setupCard.Controls.Add(setupUnderline);
            _setupCard.Controls.Add(_labelActiveSetup);
            _setupCard.Controls.Add(_labelSetup1);
            _setupCard.Controls.Add(_textKey1);
            _setupCard.Controls.Add(_buttonSetKey1);
            _setupCard.Controls.Add(_buttonSaveSetup1);
            _setupCard.Controls.Add(_labelSetup2);
            _setupCard.Controls.Add(_textKey2);
            _setupCard.Controls.Add(_buttonSetKey2);
            _setupCard.Controls.Add(_buttonSaveSetup2);

            settingsLayout.Controls.Add(operatorFrame);
            settingsLayout.Controls.Add(operatorCaption);
            settingsLayout.Controls.Add(_movementCard);
            settingsLayout.Controls.Add(_setupCard);

            int layoutWidth = Math.Max(operatorFrame.Right, _movementCard.Right);
            int layoutHeight = Math.Max(operatorFrame.Bottom + 30, _setupCard.Bottom);
            settingsLayout.Size = new Size(layoutWidth, layoutHeight);

            content.Controls.Add(settingsLayout);

            // WATERMARK logo bottom-right on settings page
            _settingsLogoWatermark = new PictureBox
            {
                Size = new Size(80, 80),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            if (_appLogoImage != null)
            {
                _settingsLogoWatermark.Image = _appLogoImage;
            }
            else
            {
                _settingsLogoWatermark.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(55, 65, 81), 2))
                    {
                        e.Graphics.DrawRectangle(pen, 4, 4, _settingsLogoWatermark.Width - 8, _settingsLogoWatermark.Height - 8);
                    }
                };
            }
            content.Controls.Add(_settingsLogoWatermark);

            // Center layout + position watermark on resize
            content.Resize += (s, e) =>
            {
                CenterInnerPanel(settingsLayout, content);
                PositionSettingsLogoWatermark(content);
            };
            CenterInnerPanel(settingsLayout, content);
            PositionSettingsLogoWatermark(content);

            _pageSettings.Controls.Add(content);
            _pageSettings.Controls.Add(header);

            SyncHorizontalFromSlider();
            SyncVerticalFromSlider();
        }

        // ===== About page =====
        private void BuildAboutPage()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = BgHeader
            };

            var backButton = new Button
            {
                Text = "← Back",
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                Location = new Point(15, 15)
            };
            backButton.FlatAppearance.BorderSize = 0;
            ApplyRoundedCorners(backButton, 6);
            backButton.Click += (s, e) => ShowProfilesPage();

            var titleLabel = new Label
            {
                AutoSize = true,
                Text = "About",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(backButton.Right + 10, 19)
            };

            header.Controls.Add(backButton);
            header.Controls.Add(titleLabel);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSettings
            };

            var layout = new Panel
            {
                BackColor = Color.Transparent,
                Size = new Size(500, 320)
            };

            var logoBox = new PictureBox
            {
                Size = new Size(180, 180),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            // ✅ Make About logo circular
            MakePictureCircular(logoBox);

            // ✅ Use a different logo ONLY for the About page
            try
            {
                string aboutLogoPath = Path.Combine(_imagesFolder, "AboutLogo.png");
                if (File.Exists(aboutLogoPath))
                {
                    using (var img = Image.FromFile(aboutLogoPath))
                    {
                        logoBox.Image = new Bitmap(img);
                    }
                }
            }
            catch
            {
                // ignore, we'll just fall back to the drawn placeholder
            }

            // If no custom AboutLogo.png found, draw a simple placeholder
            if (logoBox.Image == null)
            {
                logoBox.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(AccentPrimarySoft, 3))
                    {
                        e.Graphics.DrawRectangle(pen, 8, 8, logoBox.Width - 16, logoBox.Height - 16);
                    }
                };
            }

            var madeByLabel = new Label
            {
                AutoSize = true,
                Text = "Made by GAMMO",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White
            };

            var githubLink = new LinkLabel
            {
                AutoSize = true,
                Text = "GitHub: JAD-CHADLI",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                LinkColor = AccentPrimary,
                ActiveLinkColor = Color.White,
                VisitedLinkColor = AccentPrimarySoft
            };
            githubLink.LinkBehavior = LinkBehavior.HoverUnderline;
            githubLink.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/JAD-CHADLI",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show(
                        "Could not open the GitHub link.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };

            layout.Controls.Add(logoBox);
            layout.Controls.Add(madeByLabel);
            layout.Controls.Add(githubLink);

            void Relayout()
            {
                logoBox.Location = new Point(
                    (layout.Width - logoBox.Width) / 2,
                    10);

                madeByLabel.Location = new Point(
                    (layout.Width - madeByLabel.Width) / 2,
                    logoBox.Bottom + 15);

                githubLink.Location = new Point(
                    (layout.Width - githubLink.Width) / 2,
                    madeByLabel.Bottom + 10);
            }

            layout.Resize += (s, e) => Relayout();
            Relayout();

            content.Controls.Add(layout);

            content.Resize += (s, e) => CenterInnerPanel(layout, content);
            CenterInnerPanel(layout, content);

            _pageAbout.Controls.Add(content);
            _pageAbout.Controls.Add(header);
        }

        // ===== Tutorial page =====
        private void BuildTutorialPage()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = BgHeader
            };

            var backButton = new Button
            {
                Text = "← Back",
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                Location = new Point(15, 15)
            };
            backButton.FlatAppearance.BorderSize = 0;
            ApplyRoundedCorners(backButton, 6);
            backButton.Click += (s, e) => ShowProfilesPage();

            var titleLabel = new Label
            {
                AutoSize = true,
                Text = "Tutorial",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(backButton.Right + 10, 19)
            };

            header.Controls.Add(backButton);
            header.Controls.Add(titleLabel);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSettings
            };

            var layout = new Panel
            {
                BackColor = Color.Transparent,
                Size = new Size(700, 400)
            };

            // ✅ Bigger fonts here
            var tutorialTitle = new Label
            {
                AutoSize = true,
                Text = "How to use Mouse Slider Controller",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White
            };

            var tutorialBody = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(650, 0),
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                Text =
                    "1. Choose a side (Attackers / Defenders) at the top.\n" +
                    "2. Use the search box on the left or scroll to find the operator you want.\n" +
                    "3. Click a profile card to select it. Press \"Modify\" on a profile to open the settings page.\n" +
                    "4. In the Movement card, set Horizontal and Vertical speed with the sliders or by typing values.\n" +
                    "5. Choose a key for Setup 1 and Setup 2, then click \"Save 1\" / \"Save 2\" to store each setup for this profile.\n" +
                    "6. Back on the main page, press \"Start\" to arm the tool.\n" +
                    "   Hold RIGHT mouse button, then press LEFT mouse button to start the movement.\n" +
                    "7. Press your setup keys in-game to quickly switch between Setup 1 and Setup 2 for the selected profile.\n" +
                    "8. Use \"Reset profile\" to clear settings for the current operator, or \"RESET ALL\" to clear all profiles."
            };

            layout.Controls.Add(tutorialTitle);
            layout.Controls.Add(tutorialBody);

            void Relayout()
            {
                tutorialTitle.Location = new Point(
                    (layout.Width - tutorialTitle.Width) / 2,
                    10);

                tutorialBody.Location = new Point(
                    (layout.Width - tutorialBody.Width) / 2,
                    tutorialTitle.Bottom + 20);
            }

            layout.Resize += (s, e) => Relayout();
            Relayout();

            content.Controls.Add(layout);

            content.Resize += (s, e) => CenterInnerPanel(layout, content);
            CenterInnerPanel(layout, content);

            _pageTutorial.Controls.Add(content);
            _pageTutorial.Controls.Add(header);
        }

        private Button CreateFlatButton(string text, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 85, 99);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(31, 41, 55);
            ApplyRoundedCorners(btn, 6);
            return btn;
        }

        private void StyleSegmentButton(Button button, bool isActive)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Width = 110;
            button.Height = 30;
            button.Font = new Font("Segoe UI", 9F, isActive ? FontStyle.Bold : FontStyle.Regular);

            if (isActive)
            {
                button.BackColor = AccentPrimary;
                button.ForeColor = Color.White;
            }
            else
            {
                button.BackColor = CardNormalColor;
                button.ForeColor = TextMuted;
            }
        }

        private void ApplyRoundedCorners(Control control, int radius)
        {
            void UpdateRegion(object? sender, EventArgs e)
            {
                if (control.Width <= 0 || control.Height <= 0)
                    return;

                int d = radius * 2;
                var rect = new Rectangle(0, 0, control.Width, control.Height);

                using (var path = new GraphicsPath())
                {
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    control.Region = new Region(path);
                }
            }

            control.HandleCreated += UpdateRegion;
            control.Resize += UpdateRegion;
        }

        // ✅ Helper to make a PictureBox circular (for About logo)
        private void MakePictureCircular(PictureBox pb)
        {
            void UpdateRegion(object? sender, EventArgs e)
            {
                if (pb.Width <= 0 || pb.Height <= 0)
                    return;

                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(0, 0, pb.Width - 1, pb.Height - 1);
                    pb.Region = new Region(path);
                }
            }

            pb.HandleCreated += UpdateRegion;
            pb.Resize += UpdateRegion;
        }

        private void CenterInnerPanel(Control inner, Control outer)
        {
            int x = (outer.ClientSize.Width - inner.Width) / 2;
            int y = (outer.ClientSize.Height - inner.Height) / 2;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            inner.Location = new Point(x, y);
        }

        private void PositionSettingsLogoWatermark(Control content)
        {
            if (_settingsLogoWatermark == null) return;
            int margin = 18;
            int x = content.ClientSize.Width - _settingsLogoWatermark.Width - margin;
            int y = content.ClientSize.Height - _settingsLogoWatermark.Height - margin;
            if (x < margin) x = margin;
            if (y < margin) y = margin;
            _settingsLogoWatermark.Location = new Point(x, y);
        }

        // smooth drawing (reduce flicker)
        private void SetDoubleBuffered(Control c)
        {
            if (SystemInformation.TerminalServerSession)
                return;

            var property = typeof(Control).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);

            property?.SetValue(c, true, null);
        }

        // ==========================================================
        // Page switching
        // ==========================================================
        private void ShowProfilesPage()
        {
            _pageProfiles.Visible = true;
            _pageProfiles.BringToFront();
            _pageSettings.Visible = false;
            _pageAbout.Visible = false;
            _pageTutorial.Visible = false;
        }

        private void ShowSettingsPage()
        {
            if (_currentProfile == null)
                return;

            _labelEditingProfile.Text = $"Editing: {_currentProfile.Name}";
            LoadProfileImage(_currentProfile.ImageFileName);
            _pageSettings.Visible = true;
            _pageSettings.BringToFront();
            _pageProfiles.Visible = false;
            _pageAbout.Visible = false;
            _pageTutorial.Visible = false;
        }

        private void ShowAboutPage()
        {
            _pageAbout.Visible = true;
            _pageAbout.BringToFront();
            _pageProfiles.Visible = false;
            _pageSettings.Visible = false;
            _pageTutorial.Visible = false;
        }

        private void ShowTutorialPage()
        {
            _pageTutorial.Visible = true;
            _pageTutorial.BringToFront();
            _pageProfiles.Visible = false;
            _pageSettings.Visible = false;
            _pageAbout.Visible = false;
        }

        // ==========================================================
        // Create profiles (static names & images)
        // ==========================================================
        private void CreateProfiles()
        {
            // Category A – Attackers
            _profiles.Add(new Profile("A", 1, "Sledge", "A_Sledge.png"));
            _profiles.Add(new Profile("A", 2, "Thatcher", "A_Thatcher.png"));
            _profiles.Add(new Profile("A", 3, "Ash", "A_Ash.png"));
            _profiles.Add(new Profile("A", 4, "Thermite", "A_Thermite.png"));
            _profiles.Add(new Profile("A", 5, "Twitch", "A_Twitch.png"));
            _profiles.Add(new Profile("A", 6, "Montagne", "A_Montagne.png"));
            _profiles.Add(new Profile("A", 7, "Glaz", "A_Glaz.png"));
            _profiles.Add(new Profile("A", 8, "Fuze", "A_Fuze.png"));
            _profiles.Add(new Profile("A", 9, "Blitz", "A_Blitz.png"));
            _profiles.Add(new Profile("A", 10, "IQ", "A_IQ.png"));
            _profiles.Add(new Profile("A", 11, "Buck", "A_Buck.png"));
            _profiles.Add(new Profile("A", 12, "Blackbeard", "A_Blackbeard.png"));
            _profiles.Add(new Profile("A", 13, "Capitão", "A_Capitao.png"));
            _profiles.Add(new Profile("A", 14, "Hibana", "A_Hibana.png"));
            _profiles.Add(new Profile("A", 15, "Jackal", "A_Jackal.png"));
            _profiles.Add(new Profile("A", 16, "Ying", "A_Ying.png"));
            _profiles.Add(new Profile("A", 17, "Zofia", "A_Zofia.png"));
            _profiles.Add(new Profile("A", 18, "Dokkaebi", "A_Dokkaebi.png"));
            _profiles.Add(new Profile("A", 19, "Lion", "A_Lion.png"));
            _profiles.Add(new Profile("A", 20, "Finka", "A_Finka.png"));
            _profiles.Add(new Profile("A", 21, "Maverick", "A_Maverick.png"));
            _profiles.Add(new Profile("A", 22, "Nomad", "A_Nomad.png"));
            _profiles.Add(new Profile("A", 23, "Gridlock", "A_Gridlock.png"));
            _profiles.Add(new Profile("A", 24, "Nøkk", "A_Nokk.png"));
            _profiles.Add(new Profile("A", 25, "Amaru", "A_Amaru.png"));
            _profiles.Add(new Profile("A", 26, "Kali", "A_Kali.png"));
            _profiles.Add(new Profile("A", 27, "Iana", "A_Iana.png"));
            _profiles.Add(new Profile("A", 28, "Ace", "A_Ace.png"));
            _profiles.Add(new Profile("A", 29, "Zero", "A_Zero.png"));
            _profiles.Add(new Profile("A", 30, "Flores", "A_Flores.png"));
            _profiles.Add(new Profile("A", 31, "Osa", "A_Osa.png"));
            _profiles.Add(new Profile("A", 32, "Sens", "A_Sens.png"));
            _profiles.Add(new Profile("A", 33, "Grim", "A_Grim.png"));
            _profiles.Add(new Profile("A", 34, "Brava", "A_Brava.png"));
            _profiles.Add(new Profile("A", 35, "Ram", "A_Ram.png"));
            _profiles.Add(new Profile("A", 36, "Deimos", "A_Deimos.png"));
            _profiles.Add(new Profile("A", 37, "Striker", "A_Striker.png"));
            _profiles.Add(new Profile("A", 38, "Rauora", "A_Rauora.png"));

            // Category B – Defenders
            _profiles.Add(new Profile("B", 1, "Smoke", "B_Smoke.png"));
            _profiles.Add(new Profile("B", 2, "Mute", "B_Mute.png"));
            _profiles.Add(new Profile("B", 3, "Castle", "B_Castle.png"));
            _profiles.Add(new Profile("B", 4, "Pulse", "B_Pulse.png"));
            _profiles.Add(new Profile("B", 5, "Doc", "B_Doc.png"));
            _profiles.Add(new Profile("B", 6, "Rook", "B_Rook.png"));
            _profiles.Add(new Profile("B", 7, "Kapkan", "B_Kapkan.png"));
            _profiles.Add(new Profile("B", 8, "Tachanka", "B_Tachanka.png"));
            _profiles.Add(new Profile("B", 9, "Jäger", "B_Jager.png"));
            _profiles.Add(new Profile("B", 10, "Bandit", "B_Bandit.png"));
            _profiles.Add(new Profile("B", 11, "Frost", "B_Frost.png"));
            _profiles.Add(new Profile("B", 12, "Valkyrie", "B_Valkyrie.png"));
            _profiles.Add(new Profile("B", 13, "Caveira", "B_Caveira.png"));
            _profiles.Add(new Profile("B", 14, "Echo", "B_Echo.png"));
            _profiles.Add(new Profile("B", 15, "Mira", "B_Mira.png"));
            _profiles.Add(new Profile("B", 16, "Lesion", "B_Lesion.png"));
            _profiles.Add(new Profile("B", 17, "Ela", "B_Ela.png"));
            _profiles.Add(new Profile("B", 18, "Vigil", "B_Vigil.png"));
            _profiles.Add(new Profile("B", 19, "Maestro", "B_Maestro.png"));
            _profiles.Add(new Profile("B", 20, "Alibi", "B_Alibi.png"));
            _profiles.Add(new Profile("B", 21, "Clash", "B_Clash.png"));
            _profiles.Add(new Profile("B", 22, "Kaid", "B_Kaid.png"));
            _profiles.Add(new Profile("B", 23, "Mozzie", "B_Mozzie.png"));
            _profiles.Add(new Profile("B", 24, "Warden", "B_Warden.png"));
            _profiles.Add(new Profile("B", 25, "Goyo", "B_Goyo.png"));
            _profiles.Add(new Profile("B", 26, "Wamai", "B_Wamai.png"));
            _profiles.Add(new Profile("B", 27, "Oryx", "B_Oryx.png"));
            _profiles.Add(new Profile("B", 28, "Melusi", "B_Melusi.png"));
            _profiles.Add(new Profile("B", 29, "Thunderbird", "B_Thunderbird.png"));
            _profiles.Add(new Profile("B", 30, "Aruni", "B_Aruni.png"));
            _profiles.Add(new Profile("B", 31, "Azami", "B_Azami.png"));
            _profiles.Add(new Profile("B", 32, "Solis", "B_Solis.png"));
            _profiles.Add(new Profile("B", 33, "Fenrir", "B_Fenrir.png"));
            _profiles.Add(new Profile("B", 34, "Tubarão", "B_Tubarao.png"));
            _profiles.Add(new Profile("B", 35, "Skopós", "B_Skopos.png"));
            _profiles.Add(new Profile("B", 36, "Sentry", "B_Sentry.png"));
            _profiles.Add(new Profile("B", 37, "Denari", "B_Denari.png"));
        }

        // ==========================================================
        // Save / Load profiles to JSON (speeds + keys only)
        // ==========================================================
        private void SaveProfilesToFile()
        {
            try
            {
                var list = new List<ProfileData>();
                foreach (var p in _profiles)
                {
                    list.Add(new ProfileData
                    {
                        Category = p.Category,
                        Index = p.Index,
                        Horizontal1 = p.Horizontal1,
                        Vertical1 = p.Vertical1,
                        Horizontal2 = p.Horizontal2,
                        Vertical2 = p.Vertical2,
                        Key1 = p.Key1,
                        Key2 = p.Key2
                    });
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(list, options);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to save profiles:\n" + ex.Message,
                    "Save error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void LoadProfilesFromFile()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                    return;

                string json = File.ReadAllText(_dataFilePath);
                var list = JsonSerializer.Deserialize<List<ProfileData>>(json);
                if (list == null) return;

                foreach (var data in list)
                {
                    var p = _profiles.Find(p =>
                        p.Category == data.Category && p.Index == data.Index);
                    if (p == null) continue;

                    p.Horizontal1 = data.Horizontal1;
                    p.Vertical1 = data.Vertical1;
                    p.Horizontal2 = data.Horizontal2;
                    p.Vertical2 = data.Vertical2;
                    p.Key1 = data.Key1;
                    p.Key2 = data.Key2;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load profiles:\n" + ex.Message,
                    "Load error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveProfilesToFile();
            ClearPictureBoxImage();

            if (_appLogoImage != null)
            {
                _appLogoImage.Dispose();
                _appLogoImage = null;
            }
        }

        // ==========================================================
        // Category & profile CARDS
        // ==========================================================
        private void ShowCategory(string category)
        {
            _currentCategory = category;

            // reset highlight
            if (_selectedProfileCard != null)
            {
                _selectedProfileCard.BackColor = CardNormalColor;
                _selectedProfileCard = null;
            }

            _currentProfile = null;
            _labelSelectedProfile.Text = "Selected profile: (none)";
            _labelSelectedSetup.Text = "Setup: (none)";

            if (_textKey1 != null) _textKey1.Text = "None";
            if (_textKey2 != null) _textKey2.Text = "None";
            _currentSetupIndex = 1;
            if (_labelActiveSetup != null) _labelActiveSetup.Text = "Active setup: 1";

            ClearPictureBoxImage();

            StyleSegmentButton(_buttonCategoryA, category == "A");
            StyleSegmentButton(_buttonCategoryB, category == "B");

            // reset search placeholder when switching side
            if (_searchBox != null)
            {
                _searchHasPlaceholder = true;
                _searchBox.ForeColor = TextMuted;
                _searchBox.Text = SearchPlaceholder;
            }
            _currentSearchText = string.Empty;

            RefreshProfileCards();
        }

        // (2a) search + category filtering
        private void RefreshProfileCards()
        {
            if (_profilesPanel == null)
                return;

            _profilesPanel.SuspendLayout();
            _profilesPanel.Controls.Clear();
            _profilesScrollOffset = 0;

            string search = _currentSearchText?.Trim() ?? string.Empty;
            bool hasSearch = search.Length > 0;

            Panel? existingSelectedCard = null;

            foreach (var p in _profiles)
            {
                if (p.Category != _currentCategory)
                    continue;

                if (hasSearch &&
                    (p.Name?.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) ?? -1) < 0)
                    continue;

                var card = GetOrCreateProfileCard(p);
                _profilesPanel.Controls.Add(card);

                if (_currentProfile != null && ReferenceEquals(p, _currentProfile))
                {
                    existingSelectedCard = card;
                }
            }

            _profilesPanel.ResumeLayout();

            if (_profilesPanel.Controls.Count > 0)
            {
                if (_currentProfile == null || existingSelectedCard == null)
                {
                    if (_profilesPanel.Controls[0] is Panel firstCard &&
                        firstCard.Tag is Profile firstProfile)
                    {
                        SelectProfile(firstProfile, firstCard, goToSettings: false);
                    }
                }
                else
                {
                    HighlightSelectedCard(existingSelectedCard);
                }
            }
            else
            {
                _currentProfile = null;
                UpdateSelectedProfileDetails();
            }

            CenterProfiles();
            UpdateProfilesScrollBar();
            UpdateActiveBadges();
        }

        private void SearchBox_GotFocus(object? sender, EventArgs e)
        {
            if (_searchHasPlaceholder)
            {
                _searchHasPlaceholder = false;
                _searchBox.Text = "";
                _searchBox.ForeColor = Color.White;
            }
        }

        private void SearchBox_LostFocus(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
            {
                _searchHasPlaceholder = true;
                _searchBox.ForeColor = TextMuted;
                _searchBox.Text = SearchPlaceholder;
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            if (_searchHasPlaceholder)
            {
                _currentSearchText = string.Empty;
            }
            else
            {
                _currentSearchText = _searchBox.Text ?? string.Empty;
            }
            RefreshProfileCards();
        }

        private Panel GetOrCreateProfileCard(Profile profile)
        {
            if (_profileCardCache.TryGetValue(profile, out var cachedCard))
            {
                return cachedCard;
            }

            var newCard = CreateProfileCard(profile);
            _profileCardCache[profile] = newCard;
            return newCard;
        }

        private Panel CreateProfileCard(Profile profile)
        {
            var card = new Panel
            {
                Width = 170,
                Height = 190,
                Margin = new Padding(10),
                BackColor = CardNormalColor,
                Tag = profile,
                Cursor = Cursors.Hand
            };
            ApplyRoundedCorners(card, 8);

            var activeBadge = new Label
            {
                Name = ActiveBadgeName,
                AutoSize = false,
                Width = 60,
                Height = 18,
                Text = "ACTIVE",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                BackColor = AccentPositive,
                ForeColor = Color.White,
                Visible = false
            };
            ApplyRoundedCorners(activeBadge, 9);

            activeBadge.Location = new Point(
                (card.Width - activeBadge.Width) / 2,
                8
            );

            card.Resize += (s, e) =>
            {
                activeBadge.Location = new Point(
                    (card.Width - activeBadge.Width) / 2,
                    8
                );
            };

            var thumb = new PictureBox
            {
                Width = 150,
                Height = 100,
                Location = new Point(10, activeBadge.Bottom + 5),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 23, 42)
            };
            LoadThumbnailImage(thumb, profile.ImageFileName);

            var nameLabel = new Label
            {
                AutoSize = false,
                Width = 150,
                Height = 18,
                Location = new Point(10, thumb.Bottom + 5),
                Text = profile.Name,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var modifyButton = new Button
            {
                Text = "Modify",
                Width = 120,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                Location = new Point(
                    (card.Width - 120) / 2,
                    nameLabel.Bottom + 5
                ),
                Cursor = Cursors.Hand
            };
            modifyButton.FlatAppearance.BorderSize = 0;
            modifyButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 85, 99);
            modifyButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(31, 41, 55);
            modifyButton.Click += (s, e) => StartModifyProfile(profile, card);

            card.Controls.Add(activeBadge);
            card.Controls.Add(thumb);
            card.Controls.Add(nameLabel);
            card.Controls.Add(modifyButton);

            card.MouseClick += ProfileCard_Click;
            thumb.MouseClick += ProfileCard_Click;
            nameLabel.MouseClick += ProfileCard_Click;

            void HandleEnter(object? s, EventArgs e)
            {
                if (card != _selectedProfileCard)
                    card.BackColor = CardHoverColor;
            }

            void HandleLeave(object? s, EventArgs e)
            {
                if (card != _selectedProfileCard)
                    card.BackColor = CardNormalColor;
            }

            foreach (Control c in new Control[] { card, thumb, nameLabel, modifyButton })
            {
                c.MouseEnter += HandleEnter;
                c.MouseLeave += HandleLeave;
            }

            return card;
        }

        private void ProfileCard_Click(object? sender, MouseEventArgs e)
        {
            Control? ctrl = sender as Control;
            if (ctrl == null) return;

            Panel? card = null;
            if (ctrl is Panel panel)
                card = panel;
            else if (ctrl.Parent is Panel parentPanel)
                card = parentPanel;

            if (card?.Tag is Profile profile)
            {
                SelectProfile(profile, card, goToSettings: false);
            }
        }

        private void StartModifyProfile(Profile profile, Panel card)
        {
            SelectProfile(profile, card, goToSettings: true);
        }

        private void SelectProfile(Profile profile, Panel? card, bool goToSettings)
        {
            // If we are staying on the profiles page and the search box is focused,
            // remove focus so you don't keep typing in it by mistake.
            if (!goToSettings && _searchBox != null && _searchBox.Focused)
            {
                this.ActiveControl = null; // drop focus from the search box
            }

            _currentProfile = profile;
            HighlightSelectedCard(card);
            LoadProfile(profile);
            UpdateSelectedProfileDetails();

            if (goToSettings)
            {
                ShowSettingsPage();
            }

            UpdateActiveBadges();
        }

        private void HighlightSelectedCard(Panel? card)
        {
            if (_selectedProfileCard != null)
            {
                _selectedProfileCard.BackColor = CardNormalColor;
            }

            _selectedProfileCard = card;

            if (card != null)
            {
                card.BackColor = CardSelectedColor;
            }
        }

        private void UpdateActiveBadges()
        {
            foreach (var kv in _profileCardCache)
            {
                var profile = kv.Key;
                var card = kv.Value;

                if (!card.Controls.ContainsKey(ActiveBadgeName))
                    continue;

                var badge = card.Controls[ActiveBadgeName] as Label;
                if (badge == null) continue;

                bool show = _isActive && _currentProfile != null && ReferenceEquals(profile, _currentProfile);
                badge.Visible = show;
            }
        }

        // (2b) show active setup + speeds in top bar
        private void UpdateSelectedProfileDetails()
        {
            if (_labelSelectedProfile == null || _labelSelectedSetup == null)
                return;

            if (_currentProfile == null)
            {
                _labelSelectedProfile.Text = "Selected profile: (none)";
                _labelSelectedSetup.Text = "Setup: (none)";
                LayoutProfilesTopBar();
                return;
            }

            _labelSelectedProfile.Text = $"Selected: {_currentProfile.Name}";

            string hText = _horizontalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            string vText = _verticalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            _labelSelectedSetup.Text = $"Setup: {_currentSetupIndex} (H: {hText}, V: {vText})";

            LayoutProfilesTopBar();
        }

        private void LoadProfile(Profile profile)
        {
            _currentSetupIndex = 1;
            _labelActiveSetup.Text = "Active setup: 1";

            _textKey1.Text = profile.Key1 == Keys.None ? "None" : profile.Key1.ToString();
            _textKey2.Text = profile.Key2 == Keys.None ? "None" : profile.Key2.ToString();

            ApplyProfileSetup(profile, 1);
        }

        // ==========================================================
        // Reset All (only speeds + keys)
        // ==========================================================
        private void ButtonResetAll_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset ALL profiles' speeds and keybinds.\nNames and images stay.\nAre you sure?",
                "Reset all profiles",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            foreach (var p in _profiles)
            {
                p.Horizontal1 = 0.0;
                p.Vertical1 = 0.0;
                p.Horizontal2 = 0.0;
                p.Vertical2 = 0.0;
                p.Key1 = Keys.None;
                p.Key2 = Keys.None;
            }

            _horizontalSpeed = 0.0;
            _verticalSpeed = 0.0;
            _accumulatedX = 0.0;
            _accumulatedY = 0.0;

            _trackBarHorizontal.Value = 0;
            _trackBarVertical.Value = 0;
            UpdateHorizontalDisplay();
            UpdateVerticalDisplay();

            _currentSetupIndex = 1;
            _labelActiveSetup.Text = "Active setup: 1";
            _textKey1.Text = "None";
            _textKey2.Text = "None";

            if (_currentProfile != null)
            {
                UpdateSelectedProfileDetails();
            }

            try
            {
                if (File.Exists(_dataFilePath))
                    File.Delete(_dataFilePath);
            }
            catch
            {
            }

            MessageBox.Show(
                "All speeds and keybinds have been reset.",
                "Reset complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // Per-profile reset
        private void ButtonResetProfile_Click(object? sender, EventArgs e)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "No profile is selected.",
                    "Reset profile",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"This will reset speeds and keybinds for {_currentProfile.Name}.\nAre you sure?",
                "Reset this profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            _currentProfile.Horizontal1 = 0.0;
            _currentProfile.Vertical1 = 0.0;
            _currentProfile.Horizontal2 = 0.0;
            _currentProfile.Vertical2 = 0.0;
            _currentProfile.Key1 = Keys.None;
            _currentProfile.Key2 = Keys.None;

            _currentSetupIndex = 1;
            _labelActiveSetup.Text = "Active setup: 1";

            ApplyProfileSetup(_currentProfile, 1);

            _textKey1.Text = "None";
            _textKey2.Text = "None";

            MessageBox.Show(
                $"Profile {_currentProfile.Name} has been reset.",
                "Profile reset",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==========================================================
        // Image loading
        // ==========================================================
        private void LoadProfileImage(string? imageFileName)
        {
            ClearPictureBoxImage();

            if (string.IsNullOrWhiteSpace(imageFileName))
                return;

            try
            {
                string fullPath = Path.Combine(_imagesFolder, imageFileName);
                if (!File.Exists(fullPath))
                    return;

                using (var img = Image.FromFile(fullPath))
                {
                    _pictureProfile.Image = new Bitmap(img);
                }
            }
            catch
            {
            }
        }

        private void LoadThumbnailImage(PictureBox target, string? imageFileName)
        {
            target.Image = null;

            if (string.IsNullOrWhiteSpace(imageFileName))
                return;

            try
            {
                string fullPath = Path.Combine(_imagesFolder, imageFileName);
                if (!File.Exists(fullPath))
                    return;

                using (var img = Image.FromFile(fullPath))
                {
                    target.Image = new Bitmap(img);
                }
            }
            catch
            {
            }
        }

        private void ClearPictureBoxImage()
        {
            if (_pictureProfile.Image != null)
            {
                _pictureProfile.Image.Dispose();
                _pictureProfile.Image = null;
            }
        }

        // ==========================================================
        // Key capture
        // ==========================================================
        private void StartCapturingKey(int setupIndex)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Please select a profile first.",
                    "No profile selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _capturingKeyForSetup = setupIndex;

            if (setupIndex == 1)
                _textKey1.Text = "Press key...";
            else
                _textKey2.Text = "Press key...";
        }

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_capturingKeyForSetup == 0 || _currentProfile == null)
                return;

            Keys key = e.KeyCode;

            if (_capturingKeyForSetup == 1)
            {
                _currentProfile.Key1 = key;
                _textKey1.Text = key.ToString();
            }
            else if (_capturingKeyForSetup == 2)
            {
                _currentProfile.Key2 = key;
                _textKey2.Text = key.ToString();
            }

            _capturingKeyForSetup = 0;
            e.Handled = true;
        }

        // ==========================================================
        // Save setups for current profile
        // ==========================================================
        private void ButtonSaveSetup1_Click(object? sender, EventArgs e)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Please select a profile first.",
                    "No profile selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ApplyHorizontalText();
            ApplyVerticalText();

            _currentProfile.Horizontal1 = _horizontalSpeed;
            _currentProfile.Vertical1 = _verticalSpeed;

            ApplyProfileSetup(_currentProfile, 1);

            MessageBox.Show(
                $"Saved Setup 1 for {_currentProfile.Name}",
                "Setup 1 saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ButtonSaveSetup2_Click(object? sender, EventArgs e)
        {
            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Please select a profile first.",
                    "No profile selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ApplyHorizontalText();
            ApplyVerticalText();

            _currentProfile.Horizontal2 = _horizontalSpeed;
            _currentProfile.Vertical2 = _verticalSpeed;

            ApplyProfileSetup(_currentProfile, 2);

            MessageBox.Show(
                $"Saved Setup 2 for {_currentProfile.Name}",
                "Setup 2 saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ApplyProfileSetup(Profile profile, int setupIndex)
        {
            _currentSetupIndex = setupIndex;
            _labelActiveSetup.Text = $"Active setup: {setupIndex}";

            double h = setupIndex == 1 ? profile.Horizontal1 : profile.Horizontal2;
            double v = setupIndex == 1 ? profile.Vertical1 : profile.Vertical2;

            _horizontalSpeed = h;
            _verticalSpeed = v;

            int hTrack = (int)Math.Round(h * SliderScale);
            if (hTrack < _trackBarHorizontal.Minimum) hTrack = _trackBarHorizontal.Minimum;
            if (hTrack > _trackBarHorizontal.Maximum) hTrack = _trackBarHorizontal.Maximum;
            _trackBarHorizontal.Value = hTrack;

            int vTrack = (int)Math.Round(v * SliderScale);
            if (vTrack < _trackBarVertical.Minimum) vTrack = _trackBarVertical.Minimum;
            if (vTrack > _trackBarVertical.Maximum) vTrack = _trackBarVertical.Maximum;
            _trackBarVertical.Value = vTrack;

            UpdateHorizontalDisplay();
            UpdateVerticalDisplay();

            if (_currentProfile != null && ReferenceEquals(profile, _currentProfile))
            {
                UpdateSelectedProfileDetails();
            }
        }

        // ==========================================================
        // Horizontal / Vertical sync & parsing
        // ==========================================================
        private void SyncHorizontalFromSlider()
        {
            _horizontalSpeed = _trackBarHorizontal.Value / SliderScale;
            UpdateHorizontalDisplay();

            if (_currentProfile != null)
                UpdateSelectedProfileDetails();
        }

        private void SyncVerticalFromSlider()
        {
            _verticalSpeed = _trackBarVertical.Value / SliderScale;
            UpdateVerticalDisplay();

            if (_currentProfile != null)
                UpdateSelectedProfileDetails();
        }

        private void UpdateHorizontalDisplay()
        {
            string text = _horizontalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            _textHorizontal.Text = text;
        }

        private void UpdateVerticalDisplay()
        {
            string text = _verticalSpeed.ToString("0.000", CultureInfo.InvariantCulture);
            _textVertical.Text = text;
        }

        private void TrackBarHorizontal_Scroll(object? sender, EventArgs e)
        {
            SyncHorizontalFromSlider();
        }

        private void TrackBarVertical_Scroll(object? sender, EventArgs e)
        {
            SyncVerticalFromSlider();
        }

        private void TextHorizontal_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyHorizontalText();
            }
        }

        private void TextHorizontal_Leave(object? sender, EventArgs e)
        {
            ApplyHorizontalText();
        }

        private void TextVertical_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyVerticalText();
            }
        }

        private void TextVertical_Leave(object? sender, EventArgs e)
        {
            ApplyVerticalText();
        }

        private void ApplyHorizontalText()
        {
            if (double.TryParse(
                    _textHorizontal.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double value))
            {
                double min = _trackBarHorizontal.Minimum / SliderScale;
                double max = _trackBarHorizontal.Maximum / SliderScale;
                if (value < min) value = min;
                if (value > max) value = max;

                _horizontalSpeed = value;
                _trackBarHorizontal.Value = (int)Math.Round(value * SliderScale);
                UpdateHorizontalDisplay();

                if (_currentProfile != null)
                    UpdateSelectedProfileDetails();
            }
        }

        private void ApplyVerticalText()
        {
            if (double.TryParse(
                    _textVertical.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double value))
            {
                double min = _trackBarVertical.Minimum / SliderScale;
                double max = _trackBarVertical.Maximum / SliderScale;
                if (value < min) value = min;
                if (value > max) value = max;

                _verticalSpeed = value;
                _trackBarVertical.Value = (int)Math.Round(value * SliderScale);
                UpdateVerticalDisplay();

                if (_currentProfile != null)
                    UpdateSelectedProfileDetails();
            }
        }

        // ==========================================================
        // Start / stop & movement
        // ==========================================================
        private void ButtonStart_Click(object? sender, EventArgs e)
        {
            _isActive = !_isActive;

            if (_isActive)
            {
                _buttonStart.Text = "Stop";
                _buttonStart.BackColor = AccentDanger;
                BackColor = BgSettings;
            }
            else
            {
                _buttonStart.Text = "Start";
                _buttonStart.BackColor = AccentPrimary;
                BackColor = BgMain;
                _comboArmed = false;
                _comboActive = false;
            }

            UpdateActiveBadges();
        }

        private void MovementTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentProfile != null)
            {
                CheckKeybind(_currentProfile.Key1, ref _key1WasDown, 1);
                CheckKeybind(_currentProfile.Key2, ref _key2WasDown, 2);
            }

            MouseButtons buttons = Control.MouseButtons;

            bool rightDown = (buttons & MouseButtons.Right) == MouseButtons.Right;
            bool leftDown = (buttons & MouseButtons.Left) == MouseButtons.Left;

            if (_isActive)
            {
                if (rightDown && !leftDown)
                {
                    _comboArmed = true;
                    _comboActive = false;
                }
                else if (rightDown && leftDown && _comboArmed)
                {
                    _comboActive = true;
                }
                else if (!rightDown || !leftDown)
                {
                    _comboActive = false;
                }

                if (_comboActive)
                {
                    ApplyMouseMovement();
                }
            }
            else
            {
                _comboArmed = false;
                _comboActive = false;
            }
        }

        private bool IsKeyPressed(Keys key)
        {
            if (key == Keys.None)
                return false;

            short state = GetAsyncKeyState(key);
            return (state & 0x8000) != 0;
        }

        private void CheckKeybind(Keys key, ref bool wasDown, int setupIndex)
        {
            bool isDown = IsKeyPressed(key);

            if (isDown && !wasDown && _currentProfile != null)
            {
                ApplyProfileSetup(_currentProfile, setupIndex);
            }

            wasDown = isDown;
        }

        private void ApplyMouseMovement()
        {
            _accumulatedX += _horizontalSpeed;
            _accumulatedY += _verticalSpeed;

            int dx = (int)_accumulatedX;
            int dy = (int)_accumulatedY;

            _accumulatedX -= dx;
            _accumulatedY -= dy;

            if (dx == 0 && dy == 0)
                return;

            Point current = Cursor.Position;
            Cursor.Position = new Point(current.X + dx, current.Y + dy);
        }
    }
}

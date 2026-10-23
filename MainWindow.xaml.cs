using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AnalisePro.Models;
using AnalisePro.Services;

namespace AnalisePro
{
    public partial class MainWindow : Window
    {
        private readonly HardwareMonitorService _monitorService;
        private readonly DispatcherTimer _timer;
        private bool _isUpdating = false;
        private bool _isPaused = false;
        private bool _isStaticLoaded = false;

        private string _currentDiskPath = "C:\\";
        private CancellationTokenSource _diskScanCts;

        private readonly SolidColorBrush _brushGreen = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        private readonly SolidColorBrush _brushCyan = new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF));
        private readonly SolidColorBrush _brushOrange = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x03));
        private readonly SolidColorBrush _brushRed = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x66));
        private readonly SolidColorBrush _brushMuted = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        private readonly SolidColorBrush _sliceStroke = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x1C));

        public MainWindow()
        {
            InitializeComponent();

            _brushGreen.Freeze();
            _brushCyan.Freeze();
            _brushOrange.Freeze();
            _brushRed.Freeze();
            _brushMuted.Freeze();
            _sliceStroke.Freeze();

            _monitorService = new HardwareMonitorService();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += async (s, e) => await RefreshHardwareDataAsync();

            Loaded += async (s, e) =>
            {
                InitDiskDrivesCombo();
                await RefreshHardwareDataAsync();
                _timer.Start();

                // Trigger initial disk scan
                NavigateToDiskPath(_currentDiskPath);
            };

            Closing += (s, e) =>
            {
                _timer.Stop();
                if (_diskScanCts != null)
                {
                    _diskScanCts.Cancel();
                    _diskScanCts.Dispose();
                }
                _monitorService.Dispose();
            };
        }

        private async Task RefreshHardwareDataAsync()
        {
            if (_isUpdating || _isPaused) return;
            _isUpdating = true;

            try
            {
                var snapshot = await Task.Run(() => _monitorService.GetSnapshot());
                UpdateUi(snapshot);
            }
            catch (Exception ex)
            {
                StatusTimestamp.Text = "Erro na leitura: " + ex.Message;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateUi(SystemSnapshot snapshot)
        {
            // 1. Admin status banner
            if (snapshot.IsAdmin)
            {
                AdminBadgeBorder.Visibility = Visibility.Visible;
                BtnElevateAdmin.Visibility = Visibility.Collapsed;
            }
            else
            {
                AdminBadgeBorder.Visibility = Visibility.Collapsed;
                BtnElevateAdmin.Visibility = Visibility.Visible;
            }

            // 2. KPI: CPU Temperature
            if (snapshot.Cpu.PackageTemp.HasValue && snapshot.Cpu.PackageTemp.Value > 0)
            {
                double temp = snapshot.Cpu.PackageTemp.Value;
                KpiCpuTemp.Text = string.Format("{0:F0} °C", temp);
                KpiCpuTemp.Foreground = GetTemperatureBrush(temp);
                KpiCpuTempSub.Text = snapshot.Cpu.MaxCoreTemp.HasValue && snapshot.Cpu.MaxCoreTemp.Value > 0
                    ? string.Format("Pico: {0:F0}°C", snapshot.Cpu.MaxCoreTemp.Value)
                    : "Sensor Térmico Ativo";
            }
            else
            {
                KpiCpuTemp.Text = "-- °C";
                KpiCpuTemp.Foreground = _brushMuted;
                KpiCpuTempSub.Text = snapshot.IsAdmin ? "Aguardando sensor" : "Clique em Ativar Admin";
            }

            // 3. KPI: CPU Load (Win32 GetSystemTimes)
            KpiCpuLoad.Text = string.Format("{0:F1} %", snapshot.Cpu.TotalLoad);
            KpiCpuProgress.Value = snapshot.Cpu.TotalLoad;

            // 4. KPI: RAM
            KpiRamLoad.Text = string.Format("{0:F0} %", snapshot.Memory.UsagePercent);
            KpiRamDetails.Text = string.Format("{0:F1} / {1:F1} GB", snapshot.Memory.UsedGb, snapshot.Memory.TotalGb);

            // 5. KPI: GPU
            var mainGpu = snapshot.Gpus.FirstOrDefault();
            if (mainGpu != null)
            {
                if (mainGpu.Temperature.HasValue && mainGpu.Temperature.Value > 0)
                {
                    KpiGpuTemp.Text = string.Format("{0:F0} °C", mainGpu.Temperature.Value);
                    KpiGpuTemp.Foreground = GetTemperatureBrush(mainGpu.Temperature.Value);
                }
                else
                {
                    KpiGpuTemp.Text = mainGpu.CoreLoad.HasValue ? string.Format("{0:F0} %", mainGpu.CoreLoad.Value) : "-- °C";
                    KpiGpuTemp.Foreground = _brushCyan;
                }

                if (mainGpu.IsIntegrated)
                {
                    KpiGpuLoad.Text = string.Format("Uso: {0:F0}% (iGPU)", mainGpu.CoreLoad.HasValue ? mainGpu.CoreLoad.Value : 0);
                }
                else
                {
                    KpiGpuLoad.Text = mainGpu.CoreLoad.HasValue ? string.Format("Uso: {0:F0}%", mainGpu.CoreLoad.Value) : mainGpu.Name;
                }
            }
            else
            {
                KpiGpuTemp.Text = "--";
                KpiGpuLoad.Text = "Não detectada";
            }

            // 6. KPI: Storage
            var primaryDisk = snapshot.Disks.FirstOrDefault();
            if (primaryDisk != null)
            {
                if (primaryDisk.Temperature.HasValue && primaryDisk.Temperature.Value > 0)
                {
                    KpiDiskTemp.Text = string.Format("{0:F0} °C", primaryDisk.Temperature.Value);
                    KpiDiskTemp.Foreground = GetTemperatureBrush(primaryDisk.Temperature.Value);
                }
                else
                {
                    KpiDiskTemp.Text = string.Format("{0:F0} %", primaryDisk.UsagePercent);
                    KpiDiskTemp.Foreground = _brushOrange;
                }
                KpiDiskFree.Text = string.Format("{0:F0} GB Livres", primaryDisk.FreeGb);
            }

            // 7. TAB 1: VISÃO GERAL
            OverviewCpuName.Text = string.IsNullOrEmpty(snapshot.Cpu.Name) ? "Processador" : snapshot.Cpu.Name;
            OverviewCpuDetails.Text = string.Format("{0} núcleos / {1} threads lógicas", snapshot.Cpu.CoreCount, snapshot.Cpu.ThreadCount);
            OverviewCpuLoadVal.Text = string.Format("{0:F1} %", snapshot.Cpu.TotalLoad);
            OverviewCpuLoadBar.Value = snapshot.Cpu.TotalLoad;

            OverviewCpuTempVal.Text = (snapshot.Cpu.PackageTemp.HasValue && snapshot.Cpu.PackageTemp.Value > 0)
                ? string.Format("{0:F0} °C", snapshot.Cpu.PackageTemp.Value)
                : "--";
            OverviewCpuTempVal.Foreground = (snapshot.Cpu.PackageTemp.HasValue && snapshot.Cpu.PackageTemp.Value > 0)
                ? GetTemperatureBrush(snapshot.Cpu.PackageTemp.Value)
                : _brushMuted;

            OverviewCpuPowerVal.Text = snapshot.Cpu.PowerWatts.HasValue ? string.Format("{0:F1} W", snapshot.Cpu.PowerWatts.Value) : "--";
            OverviewCpuClockVal.Text = snapshot.Cpu.ClockMhz.HasValue ? string.Format("{0:F0} MHz", snapshot.Cpu.ClockMhz.Value) : "--";

            // RAM Overview
            OverviewRamVal.Text = string.Format("{0:F1} / {1:F1} GB ({2:F0}%)", snapshot.Memory.UsedGb, snapshot.Memory.TotalGb, snapshot.Memory.UsagePercent);
            OverviewRamBar.Value = snapshot.Memory.UsagePercent;

            OverviewVirtualRamVal.Text = string.Format("{0:F1} / {1:F1} GB ({2:F0}%)", snapshot.Memory.VirtualUsedGb, snapshot.Memory.VirtualTotalGb, snapshot.Memory.VirtualUsagePercent);
            OverviewVirtualRamBar.Value = snapshot.Memory.VirtualUsagePercent;

            // GPU Overview
            if (mainGpu != null)
            {
                OverviewGpuName.Text = mainGpu.IsIntegrated ? string.Format("{0} (Integrada)", mainGpu.Name) : mainGpu.Name;
                OverviewGpuLoadVal.Text = mainGpu.CoreLoad.HasValue ? string.Format("{0:F0} %", mainGpu.CoreLoad.Value) : "--";
                OverviewGpuTempVal.Text = (mainGpu.Temperature.HasValue && mainGpu.Temperature.Value > 0)
                    ? string.Format("{0:F0} °C", mainGpu.Temperature.Value)
                    : "--";
                OverviewGpuTempVal.Foreground = (mainGpu.Temperature.HasValue && mainGpu.Temperature.Value > 0)
                    ? GetTemperatureBrush(mainGpu.Temperature.Value)
                    : _brushMuted;
                OverviewGpuVramVal.Text = mainGpu.VramUsedMb.HasValue ? string.Format("{0:F0} MB", mainGpu.VramUsedMb.Value) : "--";
            }

            // Storage & Network Overview
            if (primaryDisk != null)
            {
                OverviewDiskSummary.Text = string.Format("{0} • {1:F1} GB usados de {2:F1} GB ({3:F0}%)", primaryDisk.Name, primaryDisk.UsedGb, primaryDisk.TotalGb, primaryDisk.UsagePercent);
                OverviewDiskBar.Value = primaryDisk.UsagePercent;
            }

            var activeNet = snapshot.Networks.FirstOrDefault();
            if (activeNet != null)
            {
                OverviewNetDownVal.Text = string.Format("{0:F1} KB/s", activeNet.DownloadSpeedKbps);
                OverviewNetUpVal.Text = string.Format("{0:F1} KB/s", activeNet.UploadSpeedKbps);
            }

            // 8. TAB 2: Hardware (Processador & Placa-Mãe)
            if (!_isStaticLoaded)
            {
                _isStaticLoaded = true;

                // CPU Specs
                CpuzName.Text = snapshot.CpuSpecs.Name;
                CpuzManufacturer.Text = snapshot.CpuSpecs.Manufacturer;
                CpuzCodename.Text = snapshot.CpuSpecs.Codename;
                CpuzSocket.Text = snapshot.CpuSpecs.Socket;
                CpuzCoresThreads.Text = string.Format("{0} Núcleos Físicos / {1} Threads Lógicas", snapshot.CpuSpecs.Cores, snapshot.CpuSpecs.Threads);
                CpuzBaseClock.Text = string.Format("{0:F0} MHz", snapshot.CpuSpecs.BaseClockMhz);
                CpuzCache.Text = string.Format("L2: {0} | L3: {1}", snapshot.CpuSpecs.L2Cache, snapshot.CpuSpecs.L3Cache);
                CpuzInstructions.Text = snapshot.CpuSpecs.Instructions;

                // Motherboard & BIOS
                BoardVendor.Text = snapshot.Motherboard.Manufacturer;
                BoardModel.Text = snapshot.Motherboard.Product;
                BoardVersion.Text = snapshot.Motherboard.Version;
                BoardSerial.Text = snapshot.Motherboard.SerialNumber;
                BiosVendor.Text = snapshot.Motherboard.BiosVendor;
                BiosVersion.Text = snapshot.Motherboard.BiosVersion;
                BiosDate.Text = snapshot.Motherboard.BiosReleaseDate;

                // RAM Modules
                RamModulesItemsControl.ItemsSource = snapshot.RamModules;

                // GPU Specs
                var gpuSpec = snapshot.GpuSpecs.FirstOrDefault();
                if (gpuSpec != null)
                {
                    GpuFullModel.Text = gpuSpec.Name;
                    GpuDriverVer.Text = gpuSpec.DriverVersion;
                    GpuResolution.Text = gpuSpec.Resolution;
                }

                // Peripherals
                PeripheralsItemsControl.ItemsSource = snapshot.Peripherals;
            }

            // Realtime Hardware Cores list
            CpuzCoresList.ItemsSource = snapshot.Cpu.Cores;

            // TAB 4: GPU Live
            if (mainGpu != null)
            {
                GpuTabTemp.Text = (mainGpu.Temperature.HasValue && mainGpu.Temperature.Value > 0)
                    ? string.Format("{0:F0} °C", mainGpu.Temperature.Value)
                    : "--";
                GpuTabTemp.Foreground = (mainGpu.Temperature.HasValue && mainGpu.Temperature.Value > 0)
                    ? GetTemperatureBrush(mainGpu.Temperature.Value)
                    : _brushMuted;
                GpuTabLoad.Text = mainGpu.CoreLoad.HasValue ? string.Format("{0:F0} %", mainGpu.CoreLoad.Value) : "--";
                GpuTabClock.Text = mainGpu.CoreClockMhz.HasValue ? string.Format("{0:F0} MHz", mainGpu.CoreClockMhz.Value) : "--";

                if (mainGpu.VramTotalMb.HasValue && mainGpu.VramTotalMb.Value > 0)
                {
                    double pct = mainGpu.VramUsagePercent.HasValue ? mainGpu.VramUsagePercent.Value : 0;
                    GpuTabVramVal.Text = string.Format("{0:F0} / {1:F0} MB ({2:F0}%)", mainGpu.VramUsedMb, mainGpu.VramTotalMb, pct);
                }
                else
                {
                    GpuTabVramVal.Text = mainGpu.VramUsedMb.HasValue ? string.Format("{0:F0} MB Alocados", mainGpu.VramUsedMb) : "--";
                }
            }

            // TAB 5: Disks Summary
            DisksSummaryItemsControl.ItemsSource = snapshot.Disks;

            // Footer
            StatusTimestamp.Text = string.Format("Leitura atualizada às {0:HH:mm:ss} • CPU: {1:F1}% • Taxa: 1.0s", snapshot.Timestamp, snapshot.Cpu.TotalLoad);
        }

        // ================= DISK ANALYZER & PIE CHART =================

        private void InitDiskDrivesCombo()
        {
            try
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                    .Select(d => d.Name)
                    .ToList();

                DiskDriveCombo.ItemsSource = drives;
                if (drives.Count > 0)
                {
                    DiskDriveCombo.SelectedIndex = 0;
                    _currentDiskPath = drives[0];
                }
            }
            catch { }
        }

        private void DiskDriveCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && DiskDriveCombo != null && DiskDriveCombo.SelectedItem != null)
            {
                string drive = DiskDriveCombo.SelectedItem.ToString();
                NavigateToDiskPath(drive);
            }
        }

        private void NavigateToDiskPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (_diskScanCts != null)
            {
                _diskScanCts.Cancel();
                _diskScanCts.Dispose();
            }

            _diskScanCts = new CancellationTokenSource();
            _currentDiskPath = path;
            DiskCurrentPathText.Text = path;
            DiskScanningStatus.Text = "Analisando...";
            DiskCenterSizeText.Text = "...";

            var token = _diskScanCts.Token;

            DiskUsageScanner.ScanPathAsync(path, token).ContinueWith(t =>
            {
                if (token.IsCancellationRequested || t.IsFaulted) return;

                Dispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        var result = t.Result;
                        DiskScanningStatus.Text = "";
                        DiskCenterSizeText.Text = result.TotalSizeDisplay;
                        DiskItemsListBox.ItemsSource = result.Items;
                        DrawDonutChart(DiskPieCanvas, result.Items, result.TotalSizeBytes);
                    }
                    catch { }
                }));
            });
        }

        private void DrawDonutChart(Canvas canvas, List<DiskDirectoryItem> items, long totalBytes)
        {
            try
            {
                canvas.Children.Clear();

                if (items == null || items.Count == 0 || totalBytes == 0)
                {
                    var emptyCircle = new Ellipse
                    {
                        Width = 220,
                        Height = 220,
                        Stroke = new SolidColorBrush(Color.FromRgb(0x22, 0x26, 0x38)),
                        StrokeThickness = 45,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Canvas.SetLeft(emptyCircle, 20);
                    Canvas.SetTop(emptyCircle, 20);
                    canvas.Children.Add(emptyCircle);
                    return;
                }

                double cx = 130;
                double cy = 130;
                double outerRadius = 110;
                double innerRadius = 65;

                // If single item has 100% or only 1 item
                if (items.Count == 1)
                {
                    var fullRing = new Ellipse
                    {
                        Width = 220,
                        Height = 220,
                        Stroke = items[0].ColorBrush,
                        StrokeThickness = outerRadius - innerRadius,
                        ToolTip = string.Format("{0}\n{1} (100%)\nClique para abrir esta pasta", items[0].Name, items[0].SizeDisplay),
                        Cursor = items[0].IsDirectory ? Cursors.Hand : Cursors.Arrow,
                        Tag = items[0]
                    };
                    if (items[0].IsDirectory)
                    {
                        fullRing.MouseLeftButtonDown += Slice_MouseLeftButtonDown;
                    }
                    Canvas.SetLeft(fullRing, 20);
                    Canvas.SetTop(fullRing, 20);
                    canvas.Children.Add(fullRing);
                    return;
                }

                double currentAngle = 0;

                foreach (var item in items)
                {
                    double sweep = (item.Percentage / 100.0) * 360.0;
                    if (sweep < 1.0) continue;

                    double startAngle = currentAngle;
                    double endAngle = currentAngle + sweep;
                    currentAngle += sweep;

                    // Convert to radians (0 deg is top: 12 o'clock)
                    double radStart = (startAngle - 90.0) * Math.PI / 180.0;
                    double radEnd = (endAngle - 90.0) * Math.PI / 180.0;

                    Point p1 = new Point(cx + outerRadius * Math.Cos(radStart), cy + outerRadius * Math.Sin(radStart));
                    Point p2 = new Point(cx + outerRadius * Math.Cos(radEnd), cy + outerRadius * Math.Sin(radEnd));
                    Point p3 = new Point(cx + innerRadius * Math.Cos(radEnd), cy + innerRadius * Math.Sin(radEnd));
                    Point p4 = new Point(cx + innerRadius * Math.Cos(radStart), cy + innerRadius * Math.Sin(radStart));

                    bool isLargeArc = sweep > 180.0;

                    PathFigure figure = new PathFigure
                    {
                        StartPoint = p1,
                        IsClosed = true,
                        IsFilled = true
                    };

                    figure.Segments.Add(new ArcSegment(p2, new Size(outerRadius, outerRadius), 0, isLargeArc, SweepDirection.Clockwise, true));
                    figure.Segments.Add(new LineSegment(p3, true));
                    figure.Segments.Add(new ArcSegment(p4, new Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, true));

                    PathGeometry geometry = new PathGeometry();
                    geometry.Figures.Add(figure);

                    System.Windows.Shapes.Path slice = new System.Windows.Shapes.Path
                    {
                        Data = geometry,
                        Fill = item.ColorBrush,
                        Stroke = _sliceStroke,
                        StrokeThickness = 2,
                        ToolTip = string.Format("{0}\n{1} ({2:F1}%)\n{3}", item.Name, item.SizeDisplay, item.Percentage, item.IsDirectory ? "Clique para abrir esta pasta" : "Arquivo"),
                        Cursor = item.IsDirectory ? Cursors.Hand : Cursors.Arrow,
                        Tag = item
                    };

                    if (item.IsDirectory)
                    {
                        slice.MouseLeftButtonDown += Slice_MouseLeftButtonDown;
                    }

                    canvas.Children.Add(slice);
                }
            }
            catch { }
        }

        private void Slice_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null && element.Tag is DiskDirectoryItem)
            {
                var item = (DiskDirectoryItem)element.Tag;
                if (item.IsDirectory && Directory.Exists(item.FullPath))
                {
                    NavigateToDiskPath(item.FullPath);
                }
            }
        }

        private void DiskItemsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = DiskItemsListBox.SelectedItem as DiskDirectoryItem;
            if (item != null && item.IsDirectory && Directory.Exists(item.FullPath))
            {
                NavigateToDiskPath(item.FullPath);
            }
        }

        private void BtnDiskUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var parent = Directory.GetParent(_currentDiskPath);
                if (parent != null)
                {
                    NavigateToDiskPath(parent.FullName);
                }
                else
                {
                    NavigateToDiskPath(System.IO.Path.GetPathRoot(_currentDiskPath));
                }
            }
            catch { }
        }

        private void BtnRefreshDisk_Click(object sender, RoutedEventArgs e)
        {
            NavigateToDiskPath(_currentDiskPath);
        }

        private void BtnOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(_currentDiskPath))
                {
                    Process.Start("explorer.exe", _currentDiskPath);
                }
            }
            catch { }
        }

        // ================= GENERAL HELPERS =================

        private SolidColorBrush GetTemperatureBrush(double tempCelsius)
        {
            if (tempCelsius < 55) return _brushGreen;
            if (tempCelsius < 72) return _brushCyan;
            if (tempCelsius < 85) return _brushOrange;
            return _brushRed;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            if (_isPaused)
            {
                PauseText.Text = "▶️ Pausado";
                PauseBorder.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x1A, 0x1A));
            }
            else
            {
                PauseText.Text = "⏸️ 1s";
                PauseBorder.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x2B));
            }
        }

        private void BtnElevateAdmin_Click(object sender, RoutedEventArgs e)
        {
            AdminHelper.RestartAsAdmin();
        }
    }
}

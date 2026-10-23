using System;
using System.Collections.Generic;
using System.Management;
using AnalisePro.Models;

namespace AnalisePro.Services
{
    public static class HardwareInfoService
    {
        private static CpuSpecDetails _cachedCpu;
        private static MotherboardDetails _cachedMotherboard;
        private static List<RamModuleDetails> _cachedRam;
        private static List<GpuSpecDetails> _cachedGpu;
        private static List<PeripheralItem> _cachedPeripherals;
        private static readonly object _syncLock = new object();

        public static void PopulateStaticHardwareSpecs(SystemSnapshot snapshot)
        {
            lock (_syncLock)
            {
                if (_cachedCpu == null)
                {
                    LoadAllHardwareSpecs();
                }

                snapshot.CpuSpecs = _cachedCpu;
                snapshot.Motherboard = _cachedMotherboard;
                snapshot.RamModules = _cachedRam;
                snapshot.GpuSpecs = _cachedGpu;
                snapshot.Peripherals = _cachedPeripherals;
            }
        }

        private static void LoadAllHardwareSpecs()
        {
            _cachedCpu = LoadCpuSpecs();
            _cachedMotherboard = LoadMotherboardSpecs();
            _cachedRam = LoadRamSpecs();
            _cachedGpu = LoadGpuSpecs();
            _cachedPeripherals = LoadPeripherals();
        }

        private static CpuSpecDetails LoadCpuSpecs()
        {
            var cpu = new CpuSpecDetails
            {
                Name = "Processador",
                Manufacturer = "Intel / AMD",
                Codename = "x86-64 Family",
                Socket = "Padrão",
                Instructions = "MMX, SSE, SSE2, SSE3, SSSE3, SSE4.1, SSE4.2, AVX, AVX2, FMA3, AES, VT-x, 64-bit"
            };

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, SocketDesignation, Description FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["Name"] != null) cpu.Name = obj["Name"].ToString().Trim();
                        if (obj["Manufacturer"] != null) cpu.Manufacturer = obj["Manufacturer"].ToString();
                        if (obj["Description"] != null) cpu.Description = obj["Description"].ToString();
                        if (obj["SocketDesignation"] != null) cpu.Socket = obj["SocketDesignation"].ToString();
                        if (obj["NumberOfCores"] != null) cpu.Cores = Convert.ToInt32(obj["NumberOfCores"]);
                        if (obj["NumberOfLogicalProcessors"] != null) cpu.Threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                        if (obj["MaxClockSpeed"] != null) cpu.BaseClockMhz = Convert.ToDouble(obj["MaxClockSpeed"]);

                        if (obj["L2CacheSize"] != null)
                        {
                            double l2Kb = Convert.ToDouble(obj["L2CacheSize"]);
                            cpu.L2Cache = l2Kb >= 1024
                                ? string.Format("{0:F1} MB ({1} KB)", l2Kb / 1024.0, l2Kb)
                                : string.Format("{0} KB", l2Kb);
                        }

                        if (obj["L3CacheSize"] != null)
                        {
                            double l3Kb = Convert.ToDouble(obj["L3CacheSize"]);
                            cpu.L3Cache = l3Kb >= 1024
                                ? string.Format("{0:F1} MB (Smart Cache)", l3Kb / 1024.0)
                                : string.Format("{0} KB", l3Kb);
                        }

                        // Determine Codename / Architecture Family
                        string nameLower = cpu.Name.ToLower();
                        if (nameLower.Contains("13th") || nameLower.Contains("14th"))
                            cpu.Codename = "Raptor Lake (10nm / Intel 7)";
                        else if (nameLower.Contains("12th"))
                            cpu.Codename = "Alder Lake (10nm / Intel 7)";
                        else if (nameLower.Contains("11th"))
                            cpu.Codename = "Tiger Lake / Rocket Lake";
                        else if (nameLower.Contains("10th"))
                            cpu.Codename = "Comet Lake / Ice Lake";
                        else if (nameLower.Contains("ryzen 7000") || nameLower.Contains("ryzen 8000"))
                            cpu.Codename = "Zen 4 (5nm / TSMC)";
                        else if (nameLower.Contains("ryzen 5000"))
                            cpu.Codename = "Zen 3 (7nm / TSMC)";
                        else if (nameLower.Contains("ryzen 3000"))
                            cpu.Codename = "Zen 2 (7nm / TSMC)";
                        else
                            cpu.Codename = "x86-64 Microarchitecture";

                        cpu.L1Cache = string.Format("{0} x 32 KB Instruções + {0} x 48 KB Dados", cpu.Cores > 0 ? cpu.Cores : 4);
                        break;
                    }
                }
            }
            catch { }

            return cpu;
        }

        private static MotherboardDetails LoadMotherboardSpecs()
        {
            var board = new MotherboardDetails
            {
                Manufacturer = "OEM / Fabricante",
                Product = "Placa-Mãe",
                Version = "1.0",
                SerialNumber = "Não informado",
                BiosVendor = "UEFI / BIOS",
                BiosVersion = "--",
                BiosReleaseDate = "--"
            };

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["Manufacturer"] != null) board.Manufacturer = obj["Manufacturer"].ToString().Trim();
                        if (obj["Product"] != null) board.Product = obj["Product"].ToString().Trim();
                        if (obj["Version"] != null) board.Version = obj["Version"].ToString().Trim();
                        if (obj["SerialNumber"] != null) board.SerialNumber = obj["SerialNumber"].ToString().Trim();
                        break;
                    }
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["Manufacturer"] != null) board.BiosVendor = obj["Manufacturer"].ToString().Trim();
                        if (obj["SMBIOSBIOSVersion"] != null) board.BiosVersion = obj["SMBIOSBIOSVersion"].ToString().Trim();
                        if (obj["ReleaseDate"] != null)
                        {
                            string rawDate = obj["ReleaseDate"].ToString();
                            if (rawDate.Length >= 8)
                            {
                                string yyyy = rawDate.Substring(0, 4);
                                string mm = rawDate.Substring(4, 2);
                                string dd = rawDate.Substring(6, 2);
                                board.BiosReleaseDate = string.Format("{0}/{1}/{2}", dd, mm, yyyy);
                            }
                            else
                            {
                                board.BiosReleaseDate = rawDate;
                            }
                        }
                        break;
                    }
                }
            }
            catch { }

            return board;
        }

        private static List<RamModuleDetails> LoadRamSpecs()
        {
            var list = new List<RamModuleDetails>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT DeviceLocator, Manufacturer, PartNumber, Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, MemoryType FROM Win32_PhysicalMemory"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var ram = new RamModuleDetails
                        {
                            Slot = obj["DeviceLocator"] != null ? obj["DeviceLocator"].ToString().Trim() : "Slot",
                            Manufacturer = obj["Manufacturer"] != null ? obj["Manufacturer"].ToString().Trim() : "OEM",
                            PartNumber = obj["PartNumber"] != null ? obj["PartNumber"].ToString().Trim() : "--",
                            MemoryType = "DDR4"
                        };

                        if (obj["Capacity"] != null)
                        {
                            double bytes = Convert.ToDouble(obj["Capacity"]);
                            ram.CapacityGb = Math.Round(bytes / (1024.0 * 1024 * 1024), 1);
                        }

                        if (obj["Speed"] != null) ram.SpeedMt = Convert.ToInt32(obj["Speed"]);
                        if (obj["ConfiguredClockSpeed"] != null) ram.ConfiguredSpeedMt = Convert.ToInt32(obj["ConfiguredClockSpeed"]);

                        int smbiosType = obj["SMBIOSMemoryType"] != null ? Convert.ToInt32(obj["SMBIOSMemoryType"]) : 0;
                        if (smbiosType == 34 || ram.SpeedMt >= 4800) ram.MemoryType = "DDR5";
                        else if (smbiosType == 26 || (ram.SpeedMt >= 2133 && ram.SpeedMt <= 3600)) ram.MemoryType = "DDR4";
                        else if (smbiosType == 24) ram.MemoryType = "DDR3";

                        list.Add(ram);
                    }
                }
            }
            catch { }

            return list;
        }

        private static List<GpuSpecDetails> LoadGpuSpecs()
        {
            var list = new List<GpuSpecDetails>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, VideoProcessor, DriverVersion, AdapterRAM, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var gpu = new GpuSpecDetails
                        {
                            Name = obj["Name"] != null ? obj["Name"].ToString().Trim() : "Placa de Vídeo",
                            VideoProcessor = obj["VideoProcessor"] != null ? obj["VideoProcessor"].ToString().Trim() : "--",
                            DriverVersion = obj["DriverVersion"] != null ? obj["DriverVersion"].ToString().Trim() : "--",
                            Manufacturer = "Intel / NVIDIA / AMD",
                            Resolution = "--"
                        };

                        if (gpu.Name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0) gpu.Manufacturer = "Intel Corporation";
                        else if (gpu.Name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) gpu.Manufacturer = "NVIDIA Corporation";
                        else if (gpu.Name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0) gpu.Manufacturer = "Advanced Micro Devices (AMD)";

                        if (obj["AdapterRAM"] != null)
                        {
                            double ramBytes = Convert.ToDouble(obj["AdapterRAM"]);
                            gpu.AdapterRamGb = Math.Round(ramBytes / (1024.0 * 1024 * 1024), 2);
                        }

                        if (obj["CurrentHorizontalResolution"] != null && obj["CurrentVerticalResolution"] != null)
                        {
                            string hz = obj["CurrentRefreshRate"] != null ? obj["CurrentRefreshRate"].ToString() + " Hz" : "";
                            gpu.Resolution = string.Format("{0} x {1} @ {2}", obj["CurrentHorizontalResolution"], obj["CurrentVerticalResolution"], hz).Trim();
                        }

                        list.Add(gpu);
                    }
                }
            }
            catch { }

            return list;
        }

        private static List<PeripheralItem> LoadPeripherals()
        {
            var list = new List<PeripheralItem>();

            // 1. Audio Devices
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, Status FROM Win32_SoundDevice"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        list.Add(new PeripheralItem
                        {
                            Category = "Dispositivo de Áudio",
                            Icon = "🔊",
                            Name = obj["Name"] != null ? obj["Name"].ToString().Trim() : "Áudio",
                            Manufacturer = obj["Manufacturer"] != null ? obj["Manufacturer"].ToString().Trim() : "OEM",
                            Status = obj["Status"] != null ? obj["Status"].ToString() : "OK"
                        });
                    }
                }
            }
            catch { }

            // 2. Pointing Devices (Mouse / Touchpad)
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer FROM Win32_PointingDevice"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        list.Add(new PeripheralItem
                        {
                            Category = "Mouse / Touchpad",
                            Icon = "🖱️",
                            Name = obj["Name"] != null ? obj["Name"].ToString().Trim() : "Dispositivo Apontador",
                            Manufacturer = obj["Manufacturer"] != null ? obj["Manufacturer"].ToString().Trim() : "HID",
                            Status = "Conectado"
                        });
                    }
                }
            }
            catch { }

            // 3. Keyboards
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Description FROM Win32_Keyboard"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        list.Add(new PeripheralItem
                        {
                            Category = "Teclado",
                            Icon = "⌨️",
                            Name = obj["Description"] != null ? obj["Description"].ToString().Trim() : (obj["Name"] != null ? obj["Name"].ToString().Trim() : "Teclado"),
                            Manufacturer = "Padrão de Entrada",
                            Status = "Ativo"
                        });
                    }
                }
            }
            catch { }

            // 4. Physical Disks
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Model, InterfaceType, Size, MediaType, Status FROM Win32_DiskDrive"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string sizeStr = "";
                        if (obj["Size"] != null)
                        {
                            double gb = Convert.ToDouble(obj["Size"]) / (1024.0 * 1024 * 1024);
                            sizeStr = string.Format(" ({0:F0} GB)", gb);
                        }

                        list.Add(new PeripheralItem
                        {
                            Category = "Unidade de Armazenamento Físico",
                            Icon = "💽",
                            Name = (obj["Model"] != null ? obj["Model"].ToString().Trim() : "Disco") + sizeStr,
                            Manufacturer = obj["InterfaceType"] != null ? obj["InterfaceType"].ToString().Trim() : "SATA/NVMe",
                            Status = obj["Status"] != null ? obj["Status"].ToString() : "OK"
                        });
                    }
                }
            }
            catch { }

            return list;
        }
    }
}

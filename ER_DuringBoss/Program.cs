using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Magic;
using System.Text.Json;
using System.Security.Cryptography;


namespace ER_DuringBoss
{
    internal class Program
    {

        static BlackMagic CreateProcessReaderLoop(int processId, string processName)
        {
            BlackMagic ER = new BlackMagic() { SetDebugPrivileges = false };

            while (true)
            {
                var process = processId != 0 ? processId : SProcess.GetProcessFromProcessName(processName);

                if (ER.OpenProcessAndThread(process))
                {
                    return ER;
                }
            }
        }

        static long SearchPtrToGameDataManOffset(BlackMagic blackMagic)
        {
            const string pattern = "48 8B 05 00 00 00 00 48 85 C0 74 05 48 8B 40 58 C3 C3";
            const string mask = "xxx????xxxxxxxxxxx";

            var codeLocation = blackMagic.FindPattern(pattern, mask);
            var offset = blackMagic.ReadInt(codeLocation + 3);
            var ptrToGameDataMan = codeLocation + offset + 7;

            return ptrToGameDataMan;
        }

        static void ReadDeathCounter(BlackMagic blackMagic, long GameDataMan)
        {
            int deathCounter = blackMagic.ReadInt(GameDataMan + 0x94);
            Console.WriteLine($"Death counter {deathCounter}");
        }

        static byte ReadIsBossFight(BlackMagic blackMagic, long GameDataMan)
        {
            return blackMagic.ReadByte(GameDataMan + 0xC0);
        }

        enum PatternState
        {
            PatternNotFound,
            PatternFound
        }

        enum BossFightState
        {
            InFight,
            NotInFight
        }
        static void PrintBanner()
        {
            Console.WriteLine(
@"            Detect Eldenring's boss fight by COB
                     Game version 1.12.3

* Thanks to GrandArchives cheat engine table for patterns *
*  Using modified version of BlackMagic to read process   *
-----------------------------------------------------------");
        }

        static void Main(string[] args)
        {

            PrintBanner();

            Console.WriteLine("Reading config file");
            var config = Config.Load();
            var stopwatch = new Stopwatch();
            int processId = 0;

            if (config.Attach)
            {
                Console.WriteLine($"Config : attach to {config.ProcessName}");
            }
            else
            {
                processId = RunGame(config);
                if (processId == 0)
                {
                    Console.WriteLine("Can't run process");
                    Environment.Exit(0);
                }
            }

            AttachToProcessAndDoWorkLoop(config, stopwatch, processId);
        }

        private static void AttachToProcessAndDoWorkLoop(Config config, Stopwatch stopwatch, int processId)
        {
            while (true)
            {
                Console.WriteLine(config.Attach ? "Searching for process" : "Waiting for process");

                var blackMagic = CreateProcessReaderLoop(processId, config.ProcessName);
                Console.WriteLine($"Process {blackMagic.ProcessId} opened");

                FindPatternAndDoWorkLoop(config, stopwatch, blackMagic);
            }
        }

        private static void FindPatternAndDoWorkLoop(Config config, Stopwatch stopwatch, BlackMagic blackMagic)
        {
            PatternState patternState = PatternState.PatternNotFound;
            BossFightState bossFightState = BossFightState.NotInFight;
            stopwatch.Reset();

            long ptrToGameDataMan = 0;
            while (true)
            {
                try
                {
                    switch (patternState)
                    {
                        case PatternState.PatternNotFound:
                            ptrToGameDataMan = FindPattern(blackMagic, ref patternState);
                            break;
                        case PatternState.PatternFound:
                            long GameDataMan = blackMagic.ReadInt64(ptrToGameDataMan);
                            if (GameDataMan == 0) // is the pointer not valid anymore?
                            {
                                Console.WriteLine("Can't read data, maybe game is in main menu?");
                                patternState = PatternState.PatternNotFound; break;  // search for pattern
                            }
                            bossFightState = TimeBossFightLoop(stopwatch, blackMagic, bossFightState, GameDataMan);
                            break;
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("Process is not open") || e.Message.Contains("ReadInt failed"))
                    {
                        if (!config.Attach)
                        {
                            Console.WriteLine("Game terminated, closing...");
                            Environment.Exit(0);
                        }

                        blackMagic.Close();
                        Console.WriteLine("Process closed, searching for new process...");
                        break;
                    }

                }
            }
        }

        private static BossFightState TimeBossFightLoop(Stopwatch stopwatch, BlackMagic blackMagic, BossFightState bossFightState, long GameDataMan)
        {
            byte readBossFight = ReadIsBossFight(blackMagic, GameDataMan);
            switch (bossFightState)
            {
                case BossFightState.NotInFight:
                    if (readBossFight == 1)
                    {
                        Console.WriteLine("Boss fight started");
                        bossFightState = BossFightState.InFight;
                        stopwatch.Start();
                    }
                    break;

                case BossFightState.InFight:
                    if (readBossFight == 0)
                    {
                        stopwatch.Stop();

                        // https://stackoverflow.com/a/9994060
                        TimeSpan t = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds);
                        string answer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                                t.Hours,
                                                t.Minutes,
                                                t.Seconds,
                                                t.Milliseconds);

                        Console.WriteLine($"Boss fight ended, {answer}");
                        bossFightState = BossFightState.NotInFight;
                    }
                    break;
            }

            return bossFightState;
        }

        private static long FindPattern(BlackMagic blackMagic, ref PatternState patternState)
        {
            long ptrToGameDataMan = SearchPtrToGameDataManOffsetLoop(blackMagic);
            long GameDataMan = blackMagic.ReadInt64(ptrToGameDataMan);
            if (GameDataMan != 0)
            {
                Console.WriteLine($"GameDataMan location {GameDataMan:x}");
                patternState = PatternState.PatternFound;
            }

            return ptrToGameDataMan;
        }

        private static int RunGame(Config config)
        {
            Console.WriteLine($"Config : run game located at {config.Fullpath}");
            Environment.SetEnvironmentVariable("SteamAppId", "1245620");

            var processStartInfo = new ProcessStartInfo()
            {
                FileName = config.Fullpath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(config.Fullpath)
            };
            var process = Process.Start(processStartInfo);
            return process == null ? 0 : process.Id;
        }

        private static long SearchPtrToGameDataManOffsetLoop(BlackMagic blackMagic)
        {
            long GameDataMan;
            while (true)
            {
                if ((GameDataMan = SearchPtrToGameDataManOffset(blackMagic)) != 0)
                {
                    break;
                }
                Thread.Sleep(1000);
            }

            return GameDataMan;
        }
    }
}

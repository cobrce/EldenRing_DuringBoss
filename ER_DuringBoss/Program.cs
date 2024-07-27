using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Magic;


namespace ER_DuringBoss
{
    internal class Program
    {

        static BlackMagic CreateProcessReaderLoop()
        {

            BlackMagic ER = new BlackMagic() { SetDebugPrivileges = false };
            const string processName = "eldenring";

            while (true)
            {
                var process = SProcess.GetProcessFromProcessName(processName);

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

        static void Main(string[] args)
        {
            Stopwatch stopwatch = new();
            Console.WriteLine(
@"            Detect Eldenring's boss fight by COB
                     Game version 1.12.3

* Thanks to GrandArchives cheat engine table for patterns *
*  Using modified version of BlackMagic to read process   *
-----------------------------------------------------------");

            while (true)
            {
                Console.WriteLine("Searching for process");

                // infinite loop
                var blackMagic = CreateProcessReaderLoop();
                Console.WriteLine($"Process {blackMagic.ProcessId} opened");

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
                                ptrToGameDataMan = SearchPtrToGameDataManOffsetLoop(blackMagic);
                                long GameDataMan = blackMagic.ReadInt64(ptrToGameDataMan);
                                if (GameDataMan != 0)
                                {
                                    Console.WriteLine($"GameDataMan location {GameDataMan:x}");
                                    patternState = PatternState.PatternFound;
                                }
                                break;
                            case PatternState.PatternFound:
                                GameDataMan = blackMagic.ReadInt64(ptrToGameDataMan);
                                if (GameDataMan == 0) // is the pointer not valid anymore?
                                {
                                    Console.WriteLine("Can't read data, maybe game is in main menu?");
                                    patternState = PatternState.PatternNotFound; break;  // search for pattern
                                }

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


                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("Process is not open") || e.Message.Contains("ReadInt failed"))
                        {
                            blackMagic.Close();
                            Console.WriteLine("Process closed, searching for new process...");
                            break;
                        }

                    }
                }
            }
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

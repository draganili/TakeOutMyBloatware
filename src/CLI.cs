using System;
using System.Linq;
using TakeOutMyBloatware.Operations;
using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware
{
    class CLI
    {
        private bool exitRequested = false;
        private readonly CLIActionList[] entries;
        private readonly RebootHandler rebootFlag;

        private static readonly Version programVersion = typeof(CLI).Assembly.GetName().Version!;

        public CLI(CLIActionList[] entries, RebootHandler rebootFlag)
        {
            this.entries = entries;
            this.rebootFlag = rebootFlag;
        }

        public void RunUntilKilled()
        {
            while (!exitRequested)
            {
                Console.Clear();
                Banner();
                PrintMenuEntries();
                CLIActionList chosenEntry = RequestUserChoice();

                Console.Clear();
                PrintTitleAndExplanation(chosenEntry);
                if (UserWantsToProceed())
                    TryPerformEntryOperation(chosenEntry);
            }
        }

        private void Banner()
        {
            Console.WriteLine(">> T a k e  O u t  M y  B l o a t w a r e  ^v^");
            Console.WriteLine($" Win10 security tool by DraganIli - version {programVersion.Major}.{programVersion.Minor}");
            Console.WriteLine("- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - ");
            Console.WriteLine();
        }

        private void PrintMenuEntries()
        {
            CLIHelpers.WriteLine("++ Available Actions for your system ++", ConsoleColor.Green);
            for (int i = 0; i < entries.Length; i++)
            {
                CLIHelpers.Write($"{i}: ", ConsoleColor.Green);
                Console.WriteLine(entries[i].Plugin);
            }
            Console.WriteLine();
        }

        private CLIActionList RequestUserChoice()
        {
            CLIActionList? chosenEntry = null;
            bool isUserInputCorrect = false;
            while (!isUserInputCorrect)
            {
                Console.Write("Select your desired action number > ");
                chosenEntry = GetEntryCorrespondingToUserInput(Console.ReadLine());
                if (chosenEntry == null)
                    CLIHelpers.WriteLine("Unexpected input. Your input should be a number between 1 and 15.", ConsoleColor.Red);
                else
                    isUserInputCorrect = true;
            }
            return chosenEntry!;
        }

        private CLIActionList? GetEntryCorrespondingToUserInput(string userInput)
        {
            bool inputIsNumeric = int.TryParse(userInput, out int entryIndex);
            if (inputIsNumeric)
                return entries.ElementAtOrDefault(entryIndex);

            return null;
        }

        private void PrintTitleAndExplanation(CLIActionList entry)
        {
            CLIHelpers.WriteLine($"<< {entry.Plugin} >>", ConsoleColor.Green);
            Console.WriteLine(entry.ProvideDescription());
        }

        private bool UserWantsToProceed()
        {
            Console.WriteLine("\nPress X to continue, or ESC key to go back to the menu.");
            return Console.ReadKey().Key == ConsoleKey.X;
        }

        private void TryPerformEntryOperation(CLIActionList entry)
        {
            try
            {
                Console.WriteLine();
                IActionHandler operation = entry.CreateNewAction(new ConsoleUserInterface());
                operation.Run();
                if (operation.IsRebootRecommended)
                {
                    CLIHelpers.WriteLine("\nA system reboot is recommended after this action.", ConsoleColor.Red);
                    rebootFlag.SetRebootRecommended();
                }

                if (entry.ShouldQuit)
                {
                    exitRequested = true;
                    return;
                }

                Console.Write("\nDone! ");
            }
            catch (Exception exc)
            {
                CLIHelpers.WriteLine($"Action failed: {exc.Message}", ConsoleColor.Red);
#if DEBUG
                CLIHelpers.WriteLine(exc.StackTrace, ConsoleColor.Red);
#endif
                Console.WriteLine();
            }

            CLIHelpers.FlushStandardInput();
            Console.WriteLine("Press ESC to return to the main menu");
            Console.ReadKey();
        }
    }
}

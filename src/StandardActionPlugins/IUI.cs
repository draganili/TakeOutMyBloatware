﻿using System;

namespace TakeOutMyBloatware.Operations
{
    public interface IMessagePrinter
    {
        void PrintMessage(string text);
        void PrintError(string text);
        void PrintWarning(string text);
        void PrintNotice(string text);
        void PrintHeading(string text);
        void PrintEmptySpace();
    }

    public interface IUI : IMessagePrinter
    {
        public enum UserChoice
        {
            Yes,
            No
        }

        UserChoice AskUserConsent(string text);

        void ThrowIfUserDenies(string text)
        {
            var choice = AskUserConsent(text);
            if (choice == UserChoice.No)
                throw new Exception("The user aborted the action.");
        }
    }
}

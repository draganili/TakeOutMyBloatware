﻿using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware.Operations
{
    public class SystemAppsRemovalEnabler : IActionHandler
    {
        private enum EditingOutcome
        {
            NoChangesMade,
            ContentWasUpdated
        }

        private const string STATE_REPOSITORY_DB_NAME = "StateRepository-Machine.srd";
        private const string STATE_REPOSITORY_DB_PATH =
            @"C:\ProgramData\Microsoft\Windows\AppRepository\" + STATE_REPOSITORY_DB_NAME;

        private /*lateinit*/ SqliteConnection dbConnection;
        private readonly IUI ui;

#nullable disable warnings
        public SystemAppsRemovalEnabler(IUI ui) => this.ui = ui;
#nullable restore warnings

        public void Run()
        {
            using (TokenHelper.Backup)
            using (TokenHelper.Restore)
            {
                string temporaryDatabaseCopy = CopyStateRepositoryDatabaseTo($"{STATE_REPOSITORY_DB_NAME}.tmp");
                try
                {
                    var outcome = EditStateRepositoryDatabase(temporaryDatabaseCopy);
                    if (outcome == EditingOutcome.ContentWasUpdated)
                        ReplaceStateRepositoryDatabaseWith(temporaryDatabaseCopy);
                    else
                        ui.PrintNotice("Original database doesn't need to be replaced: no changes have been made.");
                }
                finally
                {
                    if (File.Exists(temporaryDatabaseCopy))
                        File.Delete(temporaryDatabaseCopy);
                }
            }
        }

        private string CopyStateRepositoryDatabaseTo(string databaseCopyPath)
        {
            EnsureAppXServicesAreStopped();
            var database = new FileInfo(STATE_REPOSITORY_DB_PATH);
            FileInfo copiedDatabase = database.CopyTo(databaseCopyPath, overwrite: true);
            return copiedDatabase.FullName;
        }

        private EditingOutcome EditStateRepositoryDatabase(string databaseCopyPath)
        {
            ui.PrintHeading("Editing a temporary copy of state repository database...");
            using (dbConnection = new SqliteConnection($"Data Source={databaseCopyPath}"))
            {
                dbConnection.Open();

                // There's an AFTER UPDATE trigger on the table we want to edit that uses application-defined functions
                // and therefore would prevent us to make any change, so we need to temporarily get rid of it
                var afterUpdateTrigger = RetrieveAfterPackageUpdateTrigger();
                DeleteTrigger(afterUpdateTrigger?.Name);
                int updatedRows = EditPackageTable();
                ReAddTrigger(afterUpdateTrigger?.Sql);

                ui.PrintMessage($"Edited {updatedRows} {(updatedRows == 1 ? "row" : "rows")}.");
                return updatedRows == 0 ? EditingOutcome.NoChangesMade : EditingOutcome.ContentWasUpdated;
            }
        }

        private void ReplaceStateRepositoryDatabaseWith(string databaseCopyPath)
        {
            ui.PrintHeading("Replacing original state repository database with the edited copy...");
            EnsureAppXServicesAreStopped();
            // File.Copy can't be used to replace the file because it fails with access denied
            // even though we have Restore privilege, so we need to use File.Move instead
            File.Move(databaseCopyPath, STATE_REPOSITORY_DB_PATH, overwrite: true);
            ui.PrintMessage("Replacement successful.");
        }

        private void EnsureAppXServicesAreStopped()
        {
            ui.PrintMessage("Making sure AppX-related services are stopped before proceeding...");
            // Workaround for some rare circumstances in which attempts to stop these services fail for no apparent reason
            int currentAttempt = 1;
            bool servicesStoppedSuccessfully = false;
            while (!servicesStoppedSuccessfully)
            {
                try
                {
                    OS.StopServiceAndItsDependents("StateRepository");
                    servicesStoppedSuccessfully = true;
                }
                catch
                {
                    Debug.WriteLine($"Failed attempt {currentAttempt}.");
                    if (currentAttempt == 5)
                        throw;
                    currentAttempt++;
                    Thread.Sleep(TimeSpan.FromMilliseconds(200));
                }
            }
        }

        private (string Name, string Sql)? RetrieveAfterPackageUpdateTrigger()
        {
            using var query = new SqliteCommand(
                $"SELECT name, sql FROM sqlite_master WHERE type='trigger' AND tbl_name='Package' AND sql LIKE '%AFTER UPDATE%'",
                dbConnection
            );
            using var reader = query.ExecuteReader();
            if (!reader.HasRows)
                return null;

            reader.Read();
            return (Name: reader.GetString(0), Sql: reader.GetString(1));
        }

        private void DeleteTrigger(string? triggerName)
        {
            if (triggerName != null)
            {
                using var query = new SqliteCommand($"DROP TRIGGER {triggerName}", dbConnection);
                query.ExecuteNonQuery();
            }
        }

        private int EditPackageTable()
        {
            using var query = new SqliteCommand(
                "UPDATE Package SET IsInbox=0 WHERE IsInbox=1",
                dbConnection
            );
            int updatedRows = query.ExecuteNonQuery();
            return updatedRows;
        }

        private void ReAddTrigger(string? triggerSql)
        {
            if (triggerSql != null)
            {
                using var query = new SqliteCommand(triggerSql, dbConnection);
                query.ExecuteNonQuery();
            }
        }
    }
}

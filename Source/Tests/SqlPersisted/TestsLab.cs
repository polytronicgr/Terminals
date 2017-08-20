﻿using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Configuration;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Data.DB;
using Tests.Connections;
using Tests.FilePersisted;

namespace Tests.SqlPersisted
{
    /// <summary>
    /// Shared configured store used by all SQL persistence tests
    /// </summary>
    [DeploymentItem(DATABASE_LOG)]
    [DeploymentItem(DATABASE_MDF)]
    public class TestsLab
    {
        private const string DBF_FILE_NAME = "Terminals.mdf";

        private const string CONNECTION_STRING = @"Data Source=(localdb)\MSSQLLocalDB;AttachDbFilename={0}\Terminals.mdf;Integrated Security=True;";

        protected const string FAVORITE_NAME = "test";

        protected const string FAVORITE_SERVERNAME = "test_server";

        /// <summary>
        /// Gets sample text value to be checked in tests when used as password, user name or another tested value
        /// </summary>
        protected const string VALIDATION_VALUE = "AAA";

        /// <summary>
        /// Gets second sample text value to be checked in tests when used as password, user name or another tested value
        /// </summary>
        protected const string VALIDATION_VALUE_B = "BBB";

        internal const string DATABASE_LOG = @"..\Resources\Database\Terminals_log.LDF";

        internal const string DATABASE_MDF = @"..\Resources\Database\Terminals.mdf";

        private readonly Settings settings = Settings.Instance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Gets the data store on which tests should be performed
        /// </summary>
        internal SqlPersistence PrimaryPersistence { get; private set; }

        internal SqlPersistence SecondaryPersistence { get; private set; }

        /// <summary>
        /// Gets second connector to lab database. Used to check, if data reached the store
        /// </summary>
        internal Database CheckDatabase { get; private set; }

        internal IFavorites PrimaryFavorites
        {
            get
            {
                return this.PrimaryPersistence.Favorites;
            }
        }

        internal IFavorites SecondaryFavorites
        {
            get
            {
                return this.SecondaryPersistence.Favorites;
            }
        }

        internal IFactory PrimaryFactory { get { return this.PrimaryPersistence.Factory; } }

        /// <summary>
        /// Initializes data connectors on beginning of each test.
        /// </summary>
        protected void InitializeTestLab()
        {
            this.RemoveDatabaseFileReadOnly();
            FilePersistedTestLab.SetDefaultFileLocations();
            this.SetDeploymentDirConnectionString();

            // first reset the database password, then continue with other initializations
            this.CheckDatabase = DatabaseConnections.CreateInstance();
            this.CheckDatabase.UpdateMasterPassword(String.Empty);

            this.PrimaryPersistence = CreateSqlPersistence();
            this.SecondaryPersistence = CreateSqlPersistence();

            this.ClearTestLab(); // because of failed previous tests
        }

        private static SqlPersistence CreateSqlPersistence()
        {
            return CreateSqlPersistence(TestConnectionManager.Instance);
        }

        internal static SqlPersistence CreateSqlPersistence(ConnectionManager connectionManager)
        {
            var icons = TestConnectionManager.CreateTestFavoriteIcons();
            var persistence = new SqlPersistence(icons, connectionManager);
            persistence.Initialize();
            return persistence;
        }

        protected void SetDeploymentDirConnectionString()
        {
            AssignDeploymentDirConnectionString(this.settings, this.TestContext.DeploymentDirectory);
        }

        internal static void AssignDeploymentDirConnectionString(Settings settings, string deploymentDir)
        {
            // Atleast to be able assign new connection string. Becasue of this we need application restart after persistence is changed.
            settings.PersistenceSecurity = new SqlPersistenceSecurity(); 
            settings.ConnectionString = String.Format(CONNECTION_STRING, deploymentDir);
        }

        private void RemoveDatabaseFileReadOnly()
        {
            this.RemoveReadOnlyAttribute(DBF_FILE_NAME);
            this.RemoveReadOnlyAttribute("Terminals_log.ldf");
        }

        private void RemoveReadOnlyAttribute(string fileName)
        {
            string databaseMdf = Path.Combine(this.TestContext.DeploymentDirectory, fileName);
            File.SetAttributes(databaseMdf, FileAttributes.Normal);
        }

        /// <summary>
        /// Cleans up all tables in test database
        /// </summary>
        protected void ClearTestLab()
        {
            const string DELETE_COMMAND = @"DELETE FROM ";
            // first clear dependences from both Favorites and groups table because of constraints
            System.Data.Entity.Database checkQueries = this.CheckDatabase.Database;
            this.SetTrustWorthyOn(checkQueries);

            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "FavoritesInGroup");
            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "History");

            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "Favorites");
            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "BeforeConnectExecute");
            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "Security");
            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "DisplayOptions");
            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "Groups");

            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "CredentialBase");
            checkQueries.ExecuteSqlCommand(DELETE_COMMAND + "Credentials");
        }

        /// <summary>
        /// Apply this command to be able run dotCover from test runner environment wiht host protection.
        /// </summary>
        private void SetTrustWorthyOn(System.Data.Entity.Database checkQueries)
        {
            string mdfFile = Path.Combine(this.TestContext.DeploymentDirectory, DBF_FILE_NAME);
            string strustworthyCommand = string.Format(@"ALTER DATABASE ""{0}"" SET TRUSTWORTHY ON", mdfFile);
            checkQueries.ExecuteSqlCommand(strustworthyCommand);
        }

        /// <summary>
        /// Creates new test favorite using primary persistence. Returns this newly created instance.
        /// Doesn't add it to the persistence.
        /// </summary>
        internal DbFavorite CreateTestFavorite()
        {
            var favorite = this.PrimaryFactory.CreateFavorite() as DbFavorite;
            favorite.Name = FAVORITE_NAME;
            favorite.ServerName = FAVORITE_SERVERNAME;
            return favorite;
        }

        /// <summary>
        /// Creates test favorite and adds it to the primary persistence.
        /// Returns newly created favorite
        /// </summary>
        internal DbFavorite AddFavoriteToPrimaryPersistence()
        {
            DbFavorite favorite = this.CreateTestFavorite();
            this.PrimaryFavorites.Add(favorite);
            return favorite;
        }

        protected void AssertStoredCredentialsCount()
        {
            int storedCredentials = this.CheckDatabase.CredentialBase.Count();
            Assert.AreEqual(1, storedCredentials, "Apply credentials changed the credentials count");
        }
    }
}

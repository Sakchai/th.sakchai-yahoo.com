﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.DataProvider.Oracle;
using Oracle.ManagedDataAccess.Client;

namespace Plant.Model
{
    /// <summary>
    /// Represents the MS SQL Server data provider
    /// </summary>
    public partial class OraclePlantDataProvider : BaseDataProvider, IPlantDataProvider
    {
        #region Utils

        /// <summary>
        /// Get SQL commands from the script
        /// </summary>
        /// <param name="sql">SQL script</param>
        /// <returns>List of commands</returns>
        private static IList<string> GetCommandsFromScript(string sql)
        {
            var commands = new List<string>();

            //origin from the Microsoft.EntityFrameworkCore.Migrations.SqlServerMigrationsSqlGenerator.Generate method
            sql = Regex.Replace(sql, @"\\\r?\n", string.Empty);
            var batches = Regex.Split(sql, @"^\s*(GO[ \t]+[0-9]+|GO)(?:\s+|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            for (var i = 0; i < batches.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(batches[i]) || batches[i].StartsWith("GO", StringComparison.OrdinalIgnoreCase))
                    continue;

                var count = 1;
                if (i != batches.Length - 1 && batches[i + 1].StartsWith("GO", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(batches[i + 1], "([0-9]+)");
                    if (match.Success)
                        count = int.Parse(match.Value);
                }

                var builder = new StringBuilder();
                for (var j = 0; j < count; j++)
                {
                    builder.Append(batches[i]);
                    if (i == batches.Length - 1)
                        builder.AppendLine();
                }

                commands.Add(builder.ToString());
            }

            return commands;
        }

        protected virtual SqlConnectionStringBuilder GetConnectionStringBuilder()
        {
            var connectionString = "Data Source = (DESCRIPTION = (ADDRESS = (PROTOCOL = TCP)(HOST = localhost)(PORT = 1521))(CONNECT_DATA = (SERVER = DEDICATED)(SERVICE_NAME = orcl.QIS.local))); User Id =hr; Password =hr; ";

            return new SqlConnectionStringBuilder(connectionString);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a connection to a database
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <returns>Connection to a database</returns>
        public override IDbConnection CreateDbConnection(string connectionString = null)
        {
            //return new SqlConnection(!string.IsNullOrEmpty(connectionString) ? connectionString : CurrentConnectionString);
            return new OracleConnection(!string.IsNullOrEmpty(connectionString) ? connectionString : CurrentConnectionString);
        }

        /// <summary>
        /// Create the database
        /// </summary>
        /// <param name="collation">Collation</param>
        /// <param name="triesToConnect">Count of tries to connect to the database after creating; set 0 if no need to connect after creating</param>
        public void CreateDatabase(string collation, int triesToConnect = 10)
        {
            if (IsDatabaseExists())
                return;

            var builder = GetConnectionStringBuilder();

            //gets database name
            var databaseName = builder.InitialCatalog;

            //now create connection string to 'master' dabatase. It always exists.
            builder.InitialCatalog = "master";

            //using (var connection = new SqlConnection(builder.ConnectionString))
            using (var connection = new OracleConnection(builder.ConnectionString))
            {
                var query = $"CREATE DATABASE [{databaseName}]";
                if (!string.IsNullOrWhiteSpace(collation))
                    query = $"{query} COLLATE {collation}";

                //var command = new SqlCommand(query, connection);
                var command = new OracleCommand(query, connection);
                command.Connection.Open();

                command.ExecuteNonQuery();
            }

            //try connect
            if (triesToConnect <= 0)
                return;

            //sometimes on slow servers (hosting) there could be situations when database requires some time to be created.
            //but we have already started creation of tables and sample data.
            //as a result there is an exception thrown and the installation process cannot continue.
            //that's why we are in a cycle of "triesToConnect" times trying to connect to a database with a delay of one second.
            for (var i = 0; i <= triesToConnect; i++)
            {
                if (i == triesToConnect)
                    throw new Exception("Unable to connect to the new database. Please try one more time");

                if (!IsDatabaseExists())
                    Thread.Sleep(1000);
                else
                    break;
            }
        }

        /// <summary>
        /// Checks if the specified database exists, returns true if database exists
        /// </summary>
        /// <returns>Returns true if the database exists.</returns>
        public bool IsDatabaseExists()
        {
            try
            {
                //using (var connection = new SqlConnection(GetConnectionStringBuilder().ConnectionString))
                using (var connection = new OracleConnection(GetConnectionStringBuilder().ConnectionString))
                {
                    //just try to connect
                    connection.Open();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Execute commands from a file with SQL script against the context database
        /// </summary>
        /// <param name="fileProvider">File provider</param>
        /// <param name="filePath">Path to the file</param>
        //protected void ExecuteSqlScriptFromFile(IPlantFileProvider fileProvider, string filePath)
        //{
        //    filePath = fileProvider.MapPath(filePath);
        //    if (!fileProvider.FileExists(filePath))
        //        return;

        //    ExecuteSqlScript(fileProvider.ReadAllText(filePath, Encoding.Default));
        //}

        /// <summary>
        /// Execute commands from the SQL script
        /// </summary>
        /// <param name="sql">SQL script</param>
        public void ExecuteSqlScript(string sql)
        {
            var sqlCommands = GetCommandsFromScript(sql);

            using var currentConnection = CreateDataConnection();
            foreach (var command in sqlCommands)
                currentConnection.Execute(command);
        }

        /// <summary>
        /// Initialize database
        /// </summary>
        public void InitializeDatabase()
        {
            //var migrationManager = EngineContext.Current.Resolve<IMigrationManager>();
            //migrationManager.ApplyUpMigrations();

            ////create stored procedures 
            //var fileProvider = EngineContext.Current.Resolve<INopFileProvider>();
            //ExecuteSqlScriptFromFile(fileProvider, NopDataDefaults.SqlServerStoredProceduresFilePath);
        }

        /// <summary>
        /// Get the current identity value
        /// </summary>
        /// <typeparam name="T">Entity</typeparam>
        /// <returns>Integer identity; null if cannot get the result</returns>
        public virtual int? GetTableIdent<T>() where T : BaseEntity
        {
            using var currentConnection = CreateDataConnection();
            var tableName = currentConnection.GetTable<T>().TableName;

            var result = currentConnection.Query<decimal?>($"SELECT SIDENTITY_'{tableName}'.NEXTVAL as Value from DUAL")
                .FirstOrDefault();

            return result.HasValue ? Convert.ToInt32(result) : 1;
        }

        /// <summary>
        /// Set table identity (is supported)
        /// </summary>
        /// <typeparam name="T">Entity</typeparam>
        /// <param name="ident">Identity value</param>
        public virtual void SetTableIdent<T>(int ident) where T : BaseEntity
        {
            using var currentConnection = CreateDataConnection();
            var currentIdent = GetTableIdent<T>();
            if (!currentIdent.HasValue || ident <= currentIdent.Value)
                return;

            var tableName = currentConnection.GetTable<T>().TableName;

            currentConnection.Execute($"CREATE SEQUENCE SIDENTITY_{tableName}");
        }

        /// <summary>
        /// Creates a backup of the database
        /// </summary>
        public virtual void BackupDatabase(string fileName)
        {

        }

        /// <summary>
        /// Restores the database from a backup
        /// </summary>
        /// <param name="backupFileName">The name of the backup file</param>
        public virtual void RestoreDatabase(string backupFileName)
        {
          
        }

        /// <summary>
        /// Re-index database tables
        /// </summary>
        public virtual void ReIndexTables()
        {
        
        }

        /// <summary>
        /// Build the connection string
        /// </summary>
        /// <param name="nopConnectionString">Connection string info</param>
        /// <returns>Connection string</returns>
        public virtual string BuildConnectionString(IPlantConnectionStringInfo con)
        {
            if (con is null)
                throw new ArgumentNullException(nameof(con));

            return $"Data Source=(DESCRIPTION =(ADDRESS = (PROTOCOL = TCP)(HOST = {con.ServerName})(PORT = {con.Port}))(CONNECT_DATA =(SERVER = DEDICATED)(SERVICE_NAME = orcl.QIS.local)));User Id={con.Username};Password={con.Password};";

        }

        /// <summary>
        /// Gets the name of a foreign key
        /// </summary>
        /// <param name="foreignTable">Foreign key table</param>
        /// <param name="foreignColumn">Foreign key column name</param>
        /// <param name="primaryTable">Primary table</param>
        /// <param name="primaryColumn">Primary key column name</param>
        /// <param name="isShort">Indicates whether to use short form</param>
        /// <returns>Name of a foreign key</returns>
        public virtual string GetForeignKeyName(string foreignTable, string foreignColumn, string primaryTable, string primaryColumn, bool isShort = true)
        {
            var sb = new StringBuilder();

            sb.Append("FK_");
            sb.Append(foreignTable);
            sb.Append("_");

            sb.Append(isShort
                ? $"{foreignColumn}_{primaryTable}{primaryColumn}"
                : $"{foreignColumn}_{primaryTable}_{primaryColumn}");

            return sb.ToString();
        }

        /// <summary>
        /// Gets the name of an index
        /// </summary>
        /// <param name="targetTable">Target table name</param>
        /// <param name="targetColumn">Target column name</param>
        /// <param name="isShort">Indicates whether to use short form</param>
        /// <returns>Name of an index</returns>
        public virtual string GetIndexName(string targetTable, string targetColumn, bool isShort = true)
        {
            return $"IX_{targetTable}_{targetColumn}";
        }

        #endregion

        #region Properties

        /// <summary>
        /// Sql server data provider
        /// </summary>
        protected override IDataProvider LinqToDbDataProvider => new OracleDataProvider("OracleManaged");

        /// <summary>
        /// Gets allowed a limit input value of the data for hashing functions, returns 0 if not limited
        /// </summary>
        public int SupportedLengthOfBinaryHash { get; } = 8000;

        #endregion
    }
}

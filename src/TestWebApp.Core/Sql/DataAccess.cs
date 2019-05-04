using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Text;

namespace TestWebApp.Core.Sql
{
    public static class DataAccess
    {
        /// <summary>
        /// Create a connection from a settings.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        private static ConnectionData InnerCreateConnection(ConnectionStringSettings connectionString)
        {
            DbProviderFactory factory = DbProviderFactories.GetFactory(connectionString.ProviderName);
            return ConnectionData.CreateConnectionData(factory, connectionString.ConnectionString);
        }

        internal static ConnectionData CreateConnection(ConnectionStringSettings connection)
        {
            return InnerCreateConnection(connection ?? ConnectionString);
        }

        /// <summary>
        /// Incapsula i dati di una connessione e tiene traccia del fatto che siamo 
        /// o non siamo in una transazione globale.
        /// </summary>
        internal class ConnectionData : IDisposable
        {
            public DbProviderFactory Factory { get; set; }
            public String ConnectionString { get; set; }
            private Boolean IsWeakReference { get; set; }

            private Boolean IsCommittedOrRolledBack = false;
            private Boolean IsEnlistedInNhibernateTransaction = false;

            private DbTransaction _transaction;

            public DbTransaction Transaction
            {
                get
                {
                    if (IsEnlistedInNhibernateTransaction)
                    {
                        throw new NotSupportedException("Some code is trying to enlist into a transaction when transaction is not available");
                    }
                    return _transaction;
                }
            }

            private DbConnection _connection;

            public DbConnection Connection
            {
                get
                {
                    if (IsEnlistedInNhibernateTransaction)
                    {
                        throw new NotSupportedException("Some code is trying to enlist into a connection when transaction is not available");
                    }
                    return _connection;
                }
            }

            internal static ConnectionData CreateConnectionData(DbProviderFactory factory, String connectionString)
            {
                return new ConnectionData(factory, connectionString, false);
            }

            internal static ConnectionData CreateWeakConnectionData(ConnectionData data)
            {
                var conn = new ConnectionData(data.Factory, data.ConnectionString, true);
                conn._connection = data._connection;
                conn._transaction = data._transaction;
                conn.IsEnlistedInNhibernateTransaction = data.IsEnlistedInNhibernateTransaction;
                return conn;
            }

            private ConnectionData(DbProviderFactory factory, String connectionString, bool isWeakReference)
            {
                this.Factory = factory;
                this.ConnectionString = connectionString;
                this.IsWeakReference = isWeakReference;
            }

            internal void Commit()
            {

                if (IsWeakReference) return;
                IsCommittedOrRolledBack = true;
                if (!IsEnlistedInNhibernateTransaction && _transaction != null)
                {
                    _transaction.Commit();
                }
            }

            internal void Rollback()
            {
                if (IsWeakReference) return;
                IsCommittedOrRolledBack = true;
                if (!IsEnlistedInNhibernateTransaction && _transaction != null)
                {
                    _transaction.Rollback();
                }
            }

            public void Dispose()
            {
                if (IsWeakReference || IsEnlistedInNhibernateTransaction) return;

                using (Connection)
                using (Transaction)
                {
                    if (!IsCommittedOrRolledBack && !IsInException())
                    {
                        //Nessuno ha lanciato eccezioni, se il chiamante non ha chiamato commit lo faccio io
                        Transaction.Commit();
                    }
                }
            }

            private static Boolean IsInException()
            {
                return Marshal.GetExceptionPointers() != IntPtr.Zero ||
                         Marshal.GetExceptionCode() != 0;
            }

            /// <summary>
            /// preso un comando lo enlista alla connessione e transazione correnti.
            /// </summary>
            /// <param name="dbCommand"></param>
            internal void EnlistCommand(DbCommand dbCommand, Boolean enlistInTransation)
            {
                //la connessione è già stata creata da qualcun'altro, per
                //questa ragione enlistiamo il comando e basta
                if (_connection != null)
                {
                    dbCommand.Connection = _connection;
                    dbCommand.Transaction = _transaction;
                    return;
                }

                _connection = Factory.CreateConnection();
                _connection.ConnectionString = ConnectionString;
                _connection.Open();
                dbCommand.Connection = _connection;
                if (enlistInTransation)
                {
                    _transaction = _connection.BeginTransaction();
                    dbCommand.Transaction = Transaction;
                }
            }
        }

        //#endregion

        #region Static Initialization

        static DataAccess()
        {
            _parametersFormat = new Dictionary<Type, String>();
            _parametersFormat.Add(typeof(SqlCommand), "@{0}");
            parameterFormatByProviderName = new Dictionary<string, string>();
            parameterFormatByProviderName.Add("System.Data.SqlClient", "@{0}");
            parameterFormatByProviderName.Add("Oracle.ManagedDataAccess.Client", ":{0}");
        }

        /// <summary>
        /// Set connection string before using the class if you want to use a default
        /// connection string.
        /// </summary>
        public static ConnectionStringSettings ConnectionString { get; private set; }

        public static void SetConnectionString(String connectionString, String providerName)
        {
            ConnectionString = new ConnectionStringSettings("default", connectionString, providerName);
        }

        public static void SetSqlServerConnectionString(String connectionString)
        {
            SetConnectionString(connectionString, "System.Data.SqlClient");
        }

        #endregion

        #region Handling of connection

        /// <summary>
        /// To know at runtime the format of the parameter we need to check the 
        /// <see cref="System.Data.Common.DbConnection.GetSchema(String)"/> method. 
        /// To cache the format we use a dictionary with command type as a key and
        /// string format as value.
        /// </summary>
        private readonly static Dictionary<Type, String> _parametersFormat;

        /// <summary>
        /// In realtà oltre al tipo di comando io vorrei anche memorizzare il tipo di parametro
        /// usando il provider name, tipo se è System.Data.SqlClient allora è @{0}.
        /// </summary>
        private readonly static Dictionary<String, String> parameterFormatByProviderName;

        /// <summary>
        /// Gets the format of the parameter, to avoid query the schema the parameter
        /// format is cached with the type of the parameter
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private static String GetParameterFormat(DbCommand command, ConnectionStringSettings connection)
        {
            connection = connection ?? ConnectionString;
            if (!_parametersFormat.ContainsKey(command.GetType()))
            {
                lock (_parametersFormat)
                {
                    if (parameterFormatByProviderName.ContainsKey(connection.ProviderName))
                    {
                        _parametersFormat.Add(
                           command.GetType(),
                           parameterFormatByProviderName[connection.ProviderName]);
                    }
                    else
                    {
                        //Debbo interrogare il provider name
                        DbProviderFactory Factory = DbProviderFactories.GetFactory(connection.ProviderName);
                        using (DbConnection conn = Factory.CreateConnection())
                        {
                            conn.ConnectionString = connection.ConnectionString;
                            conn.Open();
                            _parametersFormat.Add(
                                command.GetType(),
                                conn.GetSchema("DataSourceInformation")
                                    .Rows[0]["ParameterMarkerFormat"].ToString());
                        }
                    }
                }
            }
            return _parametersFormat[command.GetType()];
        }

        #endregion

        #region Execution core

        /// <summary>
        /// Execute a sqlquery. This is the basic place where the query takes really place
        /// </summary>
        /// <param name="q">Query to be executed</param>
        /// <param name="executionCore">Function to execute</param>
        /// <param name="connection">Connection used to create </param>
        /// <param name="logException">If false it will not log the exception,
        /// exception will be always rethrow</param>
        public static void Execute(
            SqlQuery q,
            Action executionCore,
            ConnectionStringSettings connection,
            Boolean logException = true)
        {
            using (DataAccess.ConnectionData connectionData = DataAccess.CreateConnection(connection))
            {
                try
                {
                    using (q.Command)
                    {
                        connectionData.EnlistCommand(q.Command, true);
                        q.Command.CommandText = q.query.ToString();
                        Log.Logger.Debug(DumpCommand(q.Command));
                        executionCore();
                        //Now handle output parameters if any
                        if (q.OutputParamCount > 0)
                        {
                            foreach (KeyValuePair<String, OutputParameter> parameter in q.OutputParameters)
                            {
                                parameter.Value.Value = q.Command.Parameters[parameter.Value.Name].Value;
                            }
                        }
                    }
                    connectionData.Commit();
                }
                catch (Exception ex)
                {
                    if (logException)
                    {
                        Log.Error(ex, "Could not execute Query {Query}:", DumpCommand(q.Command));
                    }
                    connectionData.Rollback();
                    throw;
                }
            }
        }

        private static Object GetValueFromParameter(DbParameter parameter)
        {
            if (parameter == null || parameter.Value == null || parameter.Value == DBNull.Value)
            {
                return "NULL";
            }
            return parameter.Value;
        }

        public static String DumpCommand(DbCommand command)
        {
            if (command == null) return String.Empty;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Data Access Dumper:");

            if (command.CommandType == CommandType.StoredProcedure)
            {
                sb.Append("EXEC " + command.CommandText + " ");
                foreach (DbParameter parameter in command.Parameters)
                {
                    if (parameter.DbType == DbType.String)
                    {
                        sb.AppendFormat("{0}='{1}', ", parameter.ParameterName, GetValueFromParameter(parameter));
                    }
                    else
                    {
                        sb.AppendFormat("{0}={1}, ", parameter.ParameterName, GetValueFromParameter(parameter));
                    }
                }
                if (command.Parameters.Count > 0) sb.Length -= 2;
            }
            else
            {
                foreach (DbParameter parameter in command.Parameters)
                {
                    sb.AppendFormat("DECLARE {0} {2} = {1}\n",
                        parameter.ParameterName,
                        parameter.Value,
                       GetDeclarationTypeFromDbType(parameter.DbType));
                }
                sb.AppendLine(command.CommandText);
            }
            return sb.ToString();
        }

        private static String GetDeclarationTypeFromDbType(DbType type)
        {
            switch (type)
            {
                case DbType.Int32: return "INT";
                case DbType.Int16: return "SMALLINT";
                case DbType.Int64: return "BIGINT";
                case DbType.String: return "varchar(max)";
            }
            return type.ToString();
        }

        /// <summary>
        /// This is the core execution function, it accept a simple functor that will accept a sqlcommand
        /// the command is created in the core of the function so it really care of all the standard
        /// burden of creating connection, creating transaction and enlist command into a transaction.
        /// </summary>
        /// <param name="functionToExecute">The delegates that really executes the command.</param>
        /// <param name="connection"></param>
        /// <param name="enlistInTransation">Default to true, if you want the command not to be in 
        /// a transation, please specify false.</param>
        public static void Execute(
            Action<DbCommand, DbProviderFactory> functionToExecute,
            ConnectionStringSettings connection,
            Boolean enlistInTransation = true)
        {
            DbProviderFactory factory = GetFactory(connection);
            using (ConnectionData connectionData = CreateConnection(connection))
            {
                DbCommand command = null;
                try
                {
                    using (command = factory.CreateCommand())
                    {
                        command.CommandTimeout = 120;
                        command.CommandType = CommandType.Text;
                        connectionData.EnlistCommand(command, enlistInTransation);
                        functionToExecute(command, factory);
                    }
                    connectionData.Commit();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not execute Query {Query}:", DumpCommand(command));
                    connectionData.Rollback();
                    throw;
                }
            }
        }

        internal static DbProviderFactory GetFactory(ConnectionStringSettings connectionStringSettings)
        {
            return DbProviderFactories.GetFactory((connectionStringSettings ?? ConnectionString).ProviderName);
        }

        #endregion

        #region helper function

        /// <summary>
        /// This function Execute a command, it accepts a function with no parameter that
        /// Prepare a command to be executed. It internally use the 
        ///
        /// function that really executes the code.
        /// </summary>
        /// <typeparam name="T">return parameter type, it reflect the return type
        /// of the delegates</typeparam>
        /// <param name="functionToExecute">The function that prepares the command that should
        /// be executed with execute scalar.</param>
        /// <returns></returns>
        public static T ExecuteScalar<T>(Action<DbCommand, DbProviderFactory> functionToExecute)
        {
            T result = default(T);
            Execute((DbCommand command, DbProviderFactory factory) =>
            {
                functionToExecute(command, factory);
                object o = command.ExecuteScalar();
                //result = (T)o; //execute scalar mi ritorna un decimal...che non posso castare
                result = (T)Convert.ChangeType(o, typeof(T));
            },
            ConnectionString);
            return result;
        }

        public static List<T> ExecuteGetEntity<T>(Action<DbCommand, DbProviderFactory> functionToExecute, Func<IDataRecord, T> select)
        {
            List<T> retvalue = new List<T>();
            Execute((c, f) =>
            {
                functionToExecute(c, f);
                using (IDataReader dr = c.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        retvalue.Add(select(dr));
                    }
                }
            },
            ConnectionString);
            return retvalue;
        }

        /// <summary>
        /// Execute a command with no result.
        /// </summary>
        /// <param name="functionToExecute"></param>
        public static Int32 ExecuteNonQuery(Action<DbCommand, DbProviderFactory> functionToExecute)
        {
            Int32 result = -1;
            Execute((DbCommand command, DbProviderFactory factory) =>
            {
                functionToExecute(command, factory);
                result = command.ExecuteNonQuery();
            },
            ConnectionString);
            return result;
        }

        /// <summary>
        /// Execute a command with no result outside of any transaction
        /// </summary>
        /// <param name="rawQuery"></param>
        public static Int32 ExecuteNonQueryOutsideTransaction(
            ConnectionStringSettings connection,
            String rawQuery,
            Boolean enlistInTransation)
        {
            Int32 result = -1;
            Execute((DbCommand command, DbProviderFactory factory) =>
            {
                command.CommandText = rawQuery;
                result = command.ExecuteNonQuery();
            },
            connection,
            enlistInTransation);
            return result;
        }

        /// <summary>
        /// This is the function that permits to use a datareader without any risk
        /// to forget datareader open.
        /// </summary>
        /// <param name="commandPrepareFunction">The delegate should accepts 3 parameter, 
        /// the command to configure, a factory to create parameters, and finally another
        /// delegate of a function that returns the datareader.</param>
        public static void ExecuteReader(
            Action<DbCommand, DbProviderFactory, Func<IDataReader>> commandPrepareFunction)
        {
            Execute((DbCommand command, DbProviderFactory factory) =>
            {
                //The code to execute only assures that the eventually created datareader would be
                //closed in a finally block.
                IDataReader dr = null;
                try
                {
                    commandPrepareFunction(command, factory,
                        () =>
                        {
                            dr = command.ExecuteReader();
                            return dr;
                        });
                }
                finally
                {
                    dr?.Dispose();
                }
            },
            ConnectionString);
        }

        public static void FillDataset(
            DataTable table,
            Action<DbCommand, DbProviderFactory> commandPrepareFunction)
        {
            Execute(
                (DbCommand command, DbProviderFactory factory) =>
                {
                    commandPrepareFunction(command, factory);
                    using (DbDataAdapter da = factory.CreateDataAdapter())
                    {
                        da.SelectCommand = command;
                        da.Fill(table);
                    }
                },
            ConnectionString);
        }

        public static void ExecuteDataset<T>(
            String tableName,
            Action<DbCommand, DbProviderFactory, Func<T>> commandPrepareFunction)
            where T : DataSet, new()
        {
            Execute((DbCommand command, DbProviderFactory factory) =>
            {
                //The code to execute only assures that the eventually created datareader would be
                //closed in a finally block.
                using (T ds = new T())
                {
                    commandPrepareFunction(command, factory,
                        () =>
                        {
                            using (DbDataAdapter da = factory.CreateDataAdapter())
                            {
                                da.SelectCommand = command;
                                da.Fill(ds, tableName);
                            }
                            return ds;
                        });
                }
            },
            ConnectionString);
        }

        /// <summary>
        /// This is the function that permits to use a datareader without any risk
        /// to forget datareader open.
        /// </summary>
        /// <param name="commandPrepareFunction"></param>
        public static void ExecuteDataset(
            Action<DbCommand, DbProviderFactory, Func<DataSet>> commandPrepareFunction)
        {
            Execute((DbCommand command, DbProviderFactory factory) =>
            {
                //The code to execute only assures that the eventually created datareader would be
                //closed in a finally block.
                using (DataSet ds = new DataSet())
                {
                    commandPrepareFunction(command, factory,
                        () =>
                        {
                            using (DbDataAdapter da = factory.CreateDataAdapter())
                            {
                                da.SelectCommand = command;
                                da.Fill(ds);
                            }
                            return ds;
                        });
                }
            },
            ConnectionString);
        }

        #endregion

        #region Command filler and helpers

        /// <summary>
        /// Add a parameter to a command.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="factory"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public static void AddParameterToCommand(
            DbCommand command,
            ConnectionStringSettings connection,
            DbProviderFactory factory,
            System.Data.DbType type,
            String name,
            object value)
        {
            DbParameter param = factory.CreateParameter();
            param.DbType = type;
            param.ParameterName = GetParameterName(command, connection, name);
            param.Value = value;

            command.Parameters.Add(param);
        }

        public static string GetParameterName(DbCommand command, ConnectionStringSettings connection, string parameterName)
        {
            connection = connection ?? ConnectionString;
            return String.Format(GetParameterFormat(command, connection), parameterName);
        }

        #endregion

        #region FluentInterface

        public static SqlQuery CreateQuery(string s)
        {
            return new SqlQuery(s, CommandType.Text, GetFactory(null));
        }

        public static SqlQuery CreateQueryOn(ConnectionStringSettings connection, string s)
        {
            var query = new SqlQuery(s, CommandType.Text, GetFactory(connection));
            query.SetConnection(connection);
            return query;
        }

        #endregion
    }
}

using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TestWebApp.Core.Sql
{
    public class SqlQuery
    {
        #region Properties and constructor

        public ConnectionStringSettings Connection { get; private set; }

        internal DbCommand Command { get; set; }

        internal DbProviderFactory Factory { get; set; }

        private Dictionary<String, OutputParameter> outputParameters;

        internal Dictionary<String, OutputParameter> OutputParameters
        {
            get { return outputParameters ?? (outputParameters = new Dictionary<String, OutputParameter>()); }
        }

        internal Int32 OutputParamCount
        {
            get { return outputParameters == null ? 0 : outputParameters.Count; }
        }

        /// <summary>
        /// Execute the query and export everything to excel.
        /// </summary>
        /// <param name="excelFile"></param>
        public DataSet ExecuteDataset()
        {
            DataSet retValue = new DataSet();
            var table = retValue.Tables.Add("Result");
            Int32 counter = 1;
            DataAccess.Execute(this, () =>
            {
                try
                {
                    List<String> columns = new List<String>();
                    using (DbDataReader dr = Command.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (columns.Count == 0)
                            {
                                //Create the header
                                for (int i = 0; i < dr.FieldCount; i++)
                                {
                                    var fieldName = dr.GetName(i);
                                    if (columns.Contains(fieldName))
                                    {
                                        fieldName = fieldName + counter++;
                                    }
                                    var tableColumn = table.Columns.Add(fieldName);
                                    columns.Add(fieldName);
                                }
                            }
                            var row = table.NewRow();

                            for (int i = 0; i < dr.FieldCount; i++)
                            {
                                row[i] = dr[i];
                            }
                            table.Rows.Add(row);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unable to execute query {command}", DataAccess.DumpCommand(Command));
                }
            },
            connection: this.Connection);

            return retValue;
        }

        internal StringBuilder query = new StringBuilder();

        internal SqlQuery(string query, CommandType cmdType, DbProviderFactory factory)
        {
            Factory = factory;
            Command = Factory.CreateCommand();
            Command.CommandType = cmdType;
            Command.CommandTimeout = 1200;
            this.query.Append(query);
        }

        public SqlQuery AppendToQuery(String queryFragment)
        {
            query.Append(queryFragment);
            return this;
        }

        public SqlQuery AppendLineToQuery(string queryFragment)
        {
            query.Append("\n");
            query.Append(queryFragment);
            return this;
        }

        /// <summary>
        /// Lot of the time the caller add dynamic comma separated value, so it needs
        /// to remove the last comma or the last charachter.
        /// </summary>
        /// <param name="charToTrim"></param>
        /// <returns></returns>
        public SqlQuery TrimCharFromEnd(Char charToTrim)
        {
            Int32 newLength = query.Length;
            while (charToTrim == query[--newLength])
            {
                ;
            }

            query.Length = newLength + 1;
            return this;
        }

        /// <summary>
        /// Lot of the time the caller add dynamic comma separated value, so it needs
        /// to remove the last comma or the last charachter.
        /// </summary>
        /// <param name="charToTrim"></param>
        /// <returns></returns>
        public SqlQuery TrimCharsFromEnd(Int32 numOfCharToRemove)
        {
            query.Length -= numOfCharToRemove;
            return this;
        }

        #endregion

        #region Fluent

        public SqlQuery SetTimeout(Int32 timeoutInSeconds)
        {
            this.Command.CommandTimeout = timeoutInSeconds;
            return this;
        }

        public SqlQuery FormatQuery(String baseQuery, params Object[] paramList)
        {
            this.query.Length = 0;
            this.query.AppendFormat(baseQuery, paramList);
            return this;
        }

        #endregion

        #region Executor functions

        public T ExecuteScalar<T>()
        {
            T result = default(T);
            DataAccess.Execute(this, () =>
            {
                var tempres = Command.ExecuteScalar();
                if (tempres == null || tempres == DBNull.Value)
                {
                    result = default(T);
                }
                else
                {
                    result = (T)tempres;
                }
            },
            connection: this.Connection);

            return result;
        }

        /// <summary>
        /// Execute query with a reader-like semantic.
        /// </summary>
        /// <param name="action"></param>
        /// <returns>True se la query è andata a buon fine, false se sono avvenute eccezioni all'interno della query.</returns>
        public void ExecuteReader(Action<IDataReader> action)
        {
            DataAccess.Execute(this, () =>
            {
                using (DbDataReader dr = Command.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        action(dr);
                    }
                }
            },
            connection: this.Connection);
        }

        public void ExecuteReaderMaxRecord(
            Int32 maxRecordsToFetch,
            Action<IDataReader> action)
        {
            DataAccess.Execute(this, () =>
            {
                using (DbDataReader dr = Command.ExecuteReader())
                {
                    while (dr.Read() && maxRecordsToFetch-- >= 0)
                    {
                        action(dr);
                    }
                }
            },
            connection: this.Connection);
        }

        public void ExecuteGetSchema(Action<DataTable> action)
        {
            DataAccess.Execute(this, () =>
            {
                using (DbDataReader dr = Command.ExecuteReader(CommandBehavior.KeyInfo))
                {
                    var schema = dr.GetSchemaTable();
                    action(schema);
                }
            },
            connection: this.Connection);
        }

        public List<T> ExecuteBuildEntities<T>(Func<IDataRecord, T> entityBuilder)
        {
            return ExecuteBuildEntities<T>(entityBuilder, false);
        }

        public List<T> ExecuteBuildEntities<T>(Func<IDataRecord, T> entityBuilder, Boolean returnNullListOnError)
        {
            List<T> retvalue = new List<T>();
            DataAccess.Execute(this, () =>
            {
                try
                {
                    using (DbDataReader dr = Command.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            retvalue.Add(entityBuilder(dr));
                        }
                    }
                }
                catch (Exception ex)
                {
                    //logger.Error("Error executing " + DataAccess.DumpCommand(Command), ex);
                    retvalue = null;
                }
            },
            connection: this.Connection);
            return retvalue;
        }

        public T ExecuteBuildSingleEntity<T>(Func<IDataReader, T> entityBuilder) where T : class
        {
            T retvalue = null;
            DataAccess.Execute(this, () =>
            {
                using (DbDataReader dr = Command.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        retvalue = entityBuilder(dr);
                    }
                }
            },
            connection: this.Connection);
            return retvalue;
        }

        /// <summary>
        /// Permette di restituire i dati come lista di tipi base, restituisce solamente i dati
        /// della prima colonna del resultset.
        /// </summary>
        /// <typeparam name="T">Può essere solamente un tipo base, tipo intero, double etc.</typeparam>
        /// <returns></returns>
        public List<T> ExecuteList<T>()
        {
            List<T> retvalue = new List<T>();
            DataAccess.Execute(this, () =>
            {
                using (DbDataReader dr = Command.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        retvalue.Add(dr[0] == null || dr[0] == DBNull.Value ? default(T) : (T)dr[0]);
                    }
                }
            },
            connection: this.Connection);
            return retvalue;
        }

        /// <summary>
        /// Esegue la query, ma se la query da eccezione oppure torna un null torna il parametro che 
        /// indica il default value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T ExecuteScalarWithDefault<T>(T defaultValue)
        {
            T result = defaultValue;
            DataAccess.Execute(this, () =>
            {
                try
                {
                    Object obj = Command.ExecuteScalar();
                    if (obj != DBNull.Value)
                    {
                        result = (T)(obj ?? default(T));
                    }
                    else
                    {
                        //logger.Warn("DbNull returned for query " + query);
                    }
                }
                catch (Exception)
                {
                    result = defaultValue;
                }
            },
            connection: this.Connection);
            return result;
        }

        public Int32 ExecuteNonQuery(Boolean logException = true)
        {
            Int32 result = 0;
            DataAccess.Execute(
                this,
                () => result = Command.ExecuteNonQuery(),
                Connection,
                logException);
            return result;
        }

        public void FillDataTable(DataTable dt)
        {
            DataAccess.Execute(this, () =>
            {
                using (DbDataAdapter da = Factory.CreateDataAdapter())
                {
                    da.SelectCommand = Command;
                    da.Fill(dt);
                }
            },
            connection: this.Connection);
        }

        public void FillDataset(DataSet ds, String tableName)
        {
            DataAccess.Execute(this, () =>
            {
                using (DbDataAdapter da = Factory.CreateDataAdapter())
                {
                    da.SelectCommand = Command;
                    da.Fill(ds, tableName);
                }
            },
            connection: this.Connection);
        }

        #endregion

        #region PArameter Settings

        public SqlQuery SetStringParam(string parameterName, string value)
        {
            SetParam(parameterName, value, DbType.String);
            return this;
        }

        public SqlQuery SetList(string parameterName, IEnumerable paramList)
        {
            SetParam(parameterName, String.Join(",", paramList.OfType<Object>()), DbType.String);
            return this;
        }

        public SqlQuery SetInt64Param(string parameterName, Int64? value)
        {
            SetParam(parameterName, value, DbType.Int64);
            return this;
        }

        public SqlQuery SetInt32Param(string parameterName, Int32? value)
        {
            SetParam(parameterName, value, DbType.Int32);
            return this;
        }

        public SqlQuery SetInt32ParamWithNullValue(string parameterName, Int32 value, Int32 nullValue)
        {
            if (value != nullValue)
            {
                SetParam(parameterName, value, DbType.Int32);
            }

            return this;
        }

        public SqlQuery SetInt16Param(string parameterName, Int16 value)
        {
            SetParam(parameterName, value, DbType.Int16);
            return this;
        }

        public SqlQuery SetInt8Param(string parameterName, Byte value)
        {
            SetParam(parameterName, value, DbType.Byte);
            return this;
        }

        public SqlQuery SetSingleParam(string parameterName, Single? value)
        {
            SetParam(parameterName, value, DbType.Single);
            return this;
        }

        public SqlQuery SetDoubleParam(string parameterName, Double? value)
        {
            SetParam(parameterName, value, DbType.Single);
            return this;
        }

        public SqlQuery SetBooleanParam(string parameterName, Boolean? value)
        {
            SetParam(parameterName, value, DbType.Boolean);
            return this;
        }

        public SqlQuery SetGuidParam(string parameterName, Guid value)
        {
            SetParam(parameterName, value, DbType.Guid);
            return this;
        }

        public SqlQuery SetBooleanParam(string parameterName, Boolean value)
        {
            SetParam(parameterName, value, DbType.Boolean);
            return this;
        }

        public SqlQuery SetDateTimeParam(string parameterName, DateTime value)
        {
            SetParam(parameterName, value, DbType.DateTime);
            return this;
        }

        public SqlQuery SetDateTimeParam(string parameterName, DateTime? value)
        {
            SetParam(parameterName, value, DbType.DateTime);
            return this;
        }

        public SqlQuery SetFloatParam(string parameterName, Single value)
        {
            SetParam(parameterName, value, DbType.Single);
            return this;
        }

        public SqlQuery SetParam(string commandName, Object value, DbType? type = null)
        {
            String paramName = DataAccess.GetParameterName(Command, Connection, commandName);
            if (Command.CommandType == CommandType.Text)
            {
                query.Replace("{" + commandName + "}", paramName);
            }

            DbParameter param = Factory.CreateParameter();
            if (type != null)
            {
                param.DbType = type.Value;
            }

            param.ParameterName = paramName;
            param.Value = value ?? DBNull.Value;
            Command.Parameters.Add(param);
            return this;
        }

        public String SetOutParam(string commandName, DbType type)
        {
            String paramName = DataAccess.GetParameterName(Command, Connection, commandName);
            if (Command.CommandType == CommandType.Text)
            {
                query.Replace("{" + commandName + "}", paramName);
            }

            DbParameter param = Factory.CreateParameter();
            param.DbType = type;
            param.ParameterName = paramName;
            param.Direction = ParameterDirection.Output;
            Command.Parameters.Add(param);
            return paramName;
        }

        #endregion

        #region OutputParameter

        public SqlQuery SetInt32OutParam(string paramName)
        {
            String pname = SetOutParam(paramName, DbType.Int32);
            OutputParameters.Add(paramName, new OutputParameter(pname, typeof(Int32)));
            return this;
        }

        public SqlQuery SetInt64OutParam(string paramName)
        {
            String pname = SetOutParam(paramName, DbType.Int64);
            OutputParameters.Add(paramName, new OutputParameter(pname, typeof(Int64)));
            return this;
        }

        public T GetOutParam<T>(String paramName)
        {
            return (T)outputParameters[paramName].Value;
        }

        #endregion

        #region OrmLike

        /// <summary>
        /// Idrata una entità dove ogni nome di prorpietà però deve essere presente nel dareader corrispondente.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> Hydrate<T>() where T : new()
        {
            var properties = typeof(T).GetProperties();

            List<T> retvalue = new List<T>();
            DataAccess.Execute(this, () =>
            {
                HashSet<PropertyInfo> availableFields = null;
                using (DbDataReader dr = Command.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        if (availableFields == null)
                        {
                            availableFields = new HashSet<PropertyInfo>();
                            for (int i = 0; i < dr.FieldCount; i++)
                            {
                                var fieldName = dr.GetName(i);
                                var property = properties.FirstOrDefault(p => p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                                if (property != null)
                                {
                                    availableFields.Add(property);
                                }
                            }
                        }
                        retvalue.Add(Hydrater<T>(dr, availableFields));
                    }
                }
            },
            this.Connection);
            return retvalue;
        }

        public T HydrateSingle<T>() where T : class, new()
        {
            var properties = typeof(T).GetProperties();
            T entity = null;
            DataAccess.Execute(this, () =>
            {
                HashSet<PropertyInfo> availableFields = null;
                using (DbDataReader dr = Command.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        if (availableFields == null)
                        {
                            availableFields = new HashSet<PropertyInfo>();
                            for (int i = 0; i < dr.FieldCount; i++)
                            {
                                var fieldName = dr.GetName(i);
                                var property = properties.FirstOrDefault(p => p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                                if (property != null)
                                {
                                    availableFields.Add(property);
                                }
                            }
                        }
                        entity = Hydrater<T>(dr, availableFields);
                    }
                }
            },
            Connection);

            return entity;
        }

        private T Hydrater<T>(DbDataReader dr, HashSet<PropertyInfo> availableFields) where T : new()
        {
            T instance = new T();
            foreach (var property in availableFields)
            {
                if (dr[property.Name] != DBNull.Value)
                {
                    property.SetValue(instance, dr[property.Name], new Object[] { });
                }
            }
            return instance;
        }

        internal void SetConnection(ConnectionStringSettings connection)
        {
            this.Connection = connection;
        }

        #endregion
    }
}

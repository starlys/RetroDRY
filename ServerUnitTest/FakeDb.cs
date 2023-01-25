using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace UnitTest
{
    class FakeDbConnection : IDbConnection
    {
        public FakeDbCommand TheCommand = new();

        public string ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int ConnectionTimeout => throw new NotImplementedException();

        public string Database => throw new NotImplementedException();

        public ConnectionState State => throw new NotImplementedException();

        public IDbTransaction BeginTransaction()
        {
            return new FakeDbTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return new FakeDbTransaction();
        }

        public void ChangeDatabase(string databaseName)
        {
        }

        public void Close()
        {
        }

        public IDbCommand CreateCommand()
        {
            return TheCommand;
        }

        public void Dispose()
        {
        }

        public void Open()
        {
        }
    }

    class FakeDbCommand : IDbCommand
    {
        public FakeDbParameter TheLastParameter;
        public List<FakeDbParameter> TheParameters = new();

        public string CommandText { get; set; }
        public int CommandTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public CommandType CommandType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IDbConnection Connection { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IDataParameterCollection Parameters { get; set; } = new FakeDbParameterCollection();

        public IDbTransaction Transaction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public UpdateRowSource UpdatedRowSource { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Cancel()
        {
        }

        public IDbDataParameter CreateParameter()
        {
            TheLastParameter = new FakeDbParameter();
            TheParameters.Add(TheLastParameter);
            return TheLastParameter;
        }

        public void Dispose()
        {
        }

        public int ExecuteNonQuery()
        {
            return 1;
        }

        public IDataReader ExecuteReader()
        {
            throw new NotImplementedException();
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            throw new NotImplementedException();
        }

        public object ExecuteScalar()
        {
            return 0;
        }

        public void Prepare()
        {
        }
    }

    class FakeDbParameterCollection : IDataParameterCollection
    {
        public object this[string parameterName] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public object this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsFixedSize => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public bool IsSynchronized => throw new NotImplementedException();

        public object SyncRoot => throw new NotImplementedException();

        public int Add(object value)
        {
            return 0;
        }

        public void Clear()
        {
        }

        public bool Contains(string parameterName)
        {
            return false;
        }

        public bool Contains(object value)
        {
            return false;
        }

        public void CopyTo(Array array, int index)
        {
        }

        public IEnumerator GetEnumerator()
        {
            yield return null;
        }

        public int IndexOf(string parameterName)
        {
            return 0;
        }

        public int IndexOf(object value)
        {
            return 0;
        }

        public void Insert(int index, object value)
        {
        }

        public void Remove(object value)
        {
        }

        public void RemoveAt(string parameterName)
        {
        }

        public void RemoveAt(int index)
        {
        }
    }

    class FakeDbParameter : IDbDataParameter
    {
        public byte Precision { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public byte Scale { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int Size { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbType DbType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ParameterDirection Direction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsNullable => throw new NotImplementedException();

        public string ParameterName { get; set; }
        public string SourceColumn { get; set; }
        public DataRowVersion SourceVersion { get; set; }
        public object Value { get; set; }
    }

    class FakeDbTransaction : IDbTransaction
    {
        public IDbConnection Connection => throw new NotImplementedException();

        public IsolationLevel IsolationLevel => throw new NotImplementedException();

        public void Commit()
        {
        }

        public void Dispose()
        {
        }

        public void Rollback()
        {
        }
    }
}

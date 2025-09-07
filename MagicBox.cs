using DSO.Core.ExpressionExtensions;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace DSO.Core.MagicBox
{
    public enum TableType : int
    {
        Box = 1, Table = 2
    }
    public enum ColumnBehaviour
    {
        StrongType = 1, Free = 2
    }

    public interface IDMagicBox
    {
        int AddNull();
        int Add<T>(T value);
        int AddFunc<T>(Func<T> provider);
        int AddDelegate(Delegate provider);
        int GetCellIndex(IDBox cell);
        T Get<T>(int index);
        bool TryGet<T>(int index, out T result);
        object GetVal(int index);
        string GetString(int index);
        bool TryGetVal(int index, out object value);
        bool Set<T>(int index, T value);
        bool SetFunc<T>(int index, Func<T> provider);
        bool SetDelegate(int index, Delegate provider);
        int GetCount();
        void Clear();
        bool RemoveAt(int index);
        void Remove(IDBox item);
        DMagicBox.Enumerator GetEnumerator();
    }

    public interface IDBox
    {
        T Get<T>();
        bool TryGet<T>(out T result);
        object GetVal();
        string GetString();
        bool TryGetVal(out object value);
        void Set<T>(T value);
        void SetFunc<T>(Func<T> provider);
        void SetDelegate(Delegate provider);
        bool IsType<T>();
        Type PeekType();
    }

    public struct DBox : IDBox
    {
        private Delegate item;

        internal DBox(Delegate provider)
        {
            item = provider;
        }

        public T Get<T>()
        {
            if (TryGet<T>(out T result))
            {
                return result;
            }
            return default;
        }

        public object GetVal()
        {
            if (TryGetVal(out object result))
            {
                return result;
            }
            return default;
        }

        public bool TryGet<T>(out T result)
        {
            if (item == null)
            {
                result = default;
                return true;
            }

            if (item is Func<T> thunk)
            {
                result = thunk();
                return true;
            }

            result = default;
            return false; // listedeki delegenin tipi T değil
        }

        /// <summary>
        /// T belirtmeden çağırır; object döndürür. DynamicInvoke KULLANMAZ.
        /// Sıcak yolda tip başına derlenen invoker cache'i kullanır.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetVal(out object value)
        {
            var del = item;
            if (del is null)
            {
                value = null;
                return false;
            }

            var t = del.GetType();

            // Sadece 0 parametreli Func<T> desteklenir (sizin ekleme API'nız bunu garanti ediyor).
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Func<>))
            {
                value = null;
                return false;
            }

            var invoker = DMagicBox.InvokerCache.Get(t); // tip -> hazır invoker
            value = invoker(del);               // değer tipleri için burada boxing olur
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetString()
        {
            var del = item;

            if (del is null)
            {
                return null;
            }

            var t = del.GetType();

            // Sadece 0 parametreli Func<T> desteklenir (sizin ekleme API'nız bunu garanti ediyor).
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Func<>))
            {
                return null;
            }

            var invoker = DMagicBox.InvokerCache.GetString(t); // tip -> hazır invoker

            return invoker(del);               // değer tipleri için burada boxing olur
        }

        public void Set<T>(T value)
        {
            if (value == null)
            {
                item = null;
                return;
            }

            item = (Func<T>)(() => value);
        }

        public void SetFunc<T>(Func<T> provider)
        {
            if (provider is null) return;
            item = provider;
        }

        public void SetDelegate(Delegate provider)
        {
            if (provider is null) return;
            item = provider;
        }

        public bool IsType<T>()
        {
            return item is Func<T>;
        }

        public Type PeekType()
        {
            var d = item;
            return (d is null || !d.GetType().IsGenericType) ? null
                 : d.GetType().GenericTypeArguments[0];
        }
    }

    public class DMagicBox : IDMagicBox
    {
        private readonly List<IDBox> _items = new();

        public int AddNull()
        {
            _items.Add(null);
            return _items.Count - 1;
        }

        public int Add<T>(T value)
        {
            if (value != null)
            {
                Func<T> thunk = () => value;   // capture
                _items.Add(new DBox(thunk));
            }
            else
            {
                _items.Add(null);
            }

            return _items.Count - 1;
        }

        public int AddFunc<T>(Func<T> provider)
        {
            if (provider is null) return -1;
            _items.Add(new DBox(provider));
            return _items.Count - 1;
        }

        public int AddDelegate(Delegate provider)
        {
            if (provider is null) return -1;
            _items.Add(new DBox(provider));
            return _items.Count - 1;
        }

        public int GetCellIndex(IDBox cell)
        {
            return _items.IndexOf(cell);
        }

        public T Get<T>(int index)
        {
            if (TryGet<T>(index, out T result))
            {
                return result;
            }
            return default;
        }

        public bool TryGet<T>(int index, out T result)
        {
            if (index < 0 || index >= _items.Count)
            {
                result = default;
                return false;
            }

            if (_items[index].TryGet(out T thunk))
            {
                result = thunk;
                return true;
            }

            result = default;
            return false; // listedeki delegenin tipi T değil
        }

        public object GetVal(int index)
        {
            if (TryGetVal(index, out object result))
            {
                return result;
            }
            return default;
        }

        /// <summary>
        /// T belirtmeden çağırır; object döndürür. DynamicInvoke KULLANMAZ.
        /// Sıcak yolda tip başına derlenen invoker cache'i kullanır.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetVal(int index, out object value)
        {
            if (index < 0 || index >= _items.Count)
            {
                value = null;
                return false;
            }

            return _items[index].TryGetVal(out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetString(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return null;
            }

            return _items[index].GetString();
        }

        public bool Set<T>(int index, T value)
        {
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            if (_items[index] == null)
            {
                Func<T> thunk = () => value;
                _items[index] = new DBox(thunk);
                return true;
            }

            _items[index].Set(value);

            return true;
        }

        public bool SetFunc<T>(int index, Func<T> provider)
        {
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            if (_items[index] == null)
            {
                _items[index] = new DBox(provider);
                return true;
            }

            _items[index].SetFunc(provider);

            return true;
        }

        public bool SetDelegate(int index, Delegate provider)
        {
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            if (_items[index] == null)
            {
                _items[index] = new DBox(provider);
                return true;
            }

            _items[index].SetDelegate(provider);

            return true;
        }
        public int GetCount() => _items.Count;
        public int Count => _items.Count;

        public void Clear() => _items.Clear();

        public void Remove(IDBox item)
        {
            _items.Remove(item);
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            _items.RemoveAt(index);
            return true;
        }

        protected internal static class InvokerCache
        {
            private static readonly ConcurrentDictionary<Type, Func<Delegate, object>> _cacheObject = new();
            private static readonly ConcurrentDictionary<Type, Func<Delegate, string>> _cacheString = new();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Func<Delegate, object> Get(Type funcType) => _cacheObject.GetOrAdd(funcType, Build);
            public static Func<Delegate, string> GetString(Type funcType) => _cacheString.GetOrAdd(funcType, BuildString);

            private static Func<Delegate, object> Build(Type funcType)
            {
                // funcType kesinlikle Func<T> olmalı
                var dParam = Expression.Parameter(typeof(Delegate), "d");
                var casted = Expression.Convert(dParam, funcType);
                var invokeM = funcType.GetMethod("Invoke"); // T Invoke()
                                                            // Güvenlik: null olmayacak; yine de guard koyabilirsiniz:
                if (invokeM is null)
                    throw new InvalidOperationException("Func<T>.Invoke bulunamadı.");

                var call = Expression.Call(casted, invokeM);
                var boxed = Expression.Convert(call, typeof(object)); // dönüşte boxing
                var lambda = Expression.Lambda<Func<Delegate, object>>(boxed, dParam);
                return lambda.Compile();
            }

            private static Func<Delegate, string> BuildString(Type funcType)
            {
                // funcType kesinlikle Func<T> olmalı
                var dParam = Expression.Parameter(typeof(Delegate), "d");
                var casted = Expression.Convert(dParam, funcType);
                var invokeM = funcType.GetMethod("Invoke"); // T Invoke()

                if (invokeM is null)
                    throw new InvalidOperationException("Func<T>.Invoke bulunamadı.");

                var call = Expression.Call(casted, invokeM);

                // invokeM'in dönüş türüne göre işlem yap
                var returnType = invokeM.ReturnType;
                Expression result;

                if (returnType == typeof(string))
                {
                    // Dönüş string ise direkt kullan
                    result = call;
                }
                else if (returnType.IsValueType)
                {
                    // Değer türü ise boxing yap ve ToString çağır
                    var boxed = Expression.Convert(call, typeof(object));
                    var toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes);
                    result = Expression.Call(boxed, toStringMethod);
                }
                else
                {
                    // Referans türü ise null kontrolü ile ToString çağır
                    var toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes);
                    var nullCheck = Expression.Condition(
                        Expression.Equal(call, Expression.Constant(null)),
                        Expression.Constant(""),
                        Expression.Call(call, toStringMethod));
                    result = nullCheck;
                }

                var lambda = Expression.Lambda<Func<Delegate, string>>(result, dParam);
                return lambda.Compile();
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            private readonly DMagicBox _owner;
            private int _index;

            internal Enumerator(DMagicBox owner)
            {
                _owner = owner;
                _index = -1;
            }

            public bool MoveNext()
            {
                int next = _index + 1;
                if ((uint)next < (uint)_owner.GetCount())
                {
                    _index = next;
                    return true;
                }
                return false;
            }

            // Current bir "handle" struct döner; onun üstünden MagicBox metotlarını çağırıyoruz
            public IDBox Current => _owner._items[_index];

            public void Dispose() { }
        }
    }

    public class DDContainer
    {
        private List<DDTable> _tables;

        public IReadOnlyList<DDTable> Tables => _tables;

        public DDContainer()
        {
            _tables = new List<DDTable>();
        }

        public bool TryAddTable(string tableName, ColumnBehaviour columnBehaviour, out DDTable table)
        {
            if (_tables.Any(x => x.TableName.Equals(tableName, StringComparison.CurrentCultureIgnoreCase)))
            {
                table = null;
                return false;
            }

            table = new DDTable(tableName, columnBehaviour);

            _tables.Add(table);

            return true;
        }

        public DDTable AddTable(string tableName, ColumnBehaviour columnBehaviour)
        {
            if (!TryAddTable(tableName, columnBehaviour, out DDTable table))
            {
                return null;
            }

            return table;
        }

        public bool RemoveTable(string tableName)
        {
            var index = _tables.FindIndex(x => x.TableName == tableName);

            if (index == -1) return false;

            _tables.RemoveAt(index);

            return true;
        }

        public int Count => _tables.Count;

        public DDTable this[int index]
        {
            get
            {
                if (index < 0 || index >= _tables.Count) return null;

                return _tables.ElementAt(index);
            }
        }

        public DDTable this[string tableName]
        {
            get
            {
                return _tables.FirstOrDefault(x => x.TableName == tableName);
            }
        }
    }

    public class DDTable
    {
        public static Dictionary<Type, Dictionary<string, Delegate>> EntityCache;

        public DDTable(string tableName, ColumnBehaviour columnBehaviour)
        {
            TableName = tableName;
            TableType = TableType.Table;

            ColumnNames = new DDColumnCollection(columnBehaviour);
            Rows = new DDRowCollection(ColumnNames);
        }

        public DDRow NewRow()
        {
            return new DDRow(ColumnNames);
        }

        public string TableName { get; set; }
        public TableType TableType { get; init; }
        public ColumnBehaviour ColumnBehaviour => ColumnNames.ColumnBehaviour;

        public int ColumnCount => ColumnNames.Count;
        public int RowCount => Rows.Count;

        public DDColumnCollection ColumnNames { get; init; }

        public DDRowCollection Rows { get; init; }
    }

    public class DDColumnCollection
    {
        protected internal List<DDColumn> _columns;

        protected internal DDColumnCollection(ColumnBehaviour columnBehaviour)
        {
            _columns = new List<DDColumn>();
            ColumnBehaviour = columnBehaviour;
        }

        public bool AddColumn(string columnName, Type columnType = null)
        {
            if (_columns.Any(x => x.ColumnName.Equals(columnName, StringComparison.CurrentCultureIgnoreCase)))
            {
                return false;
            }

            _columns.Add(new DDColumn(columnName, columnType));

            return true;
        }

        public bool RemoveColumn(string columnName)
        {
            var index = _columns.FindIndex(x => x.ColumnName == columnName);

            if (index == -1) return false;

            _columns.RemoveAt(index);

            return true;
        }

        public DDColumn FirstOrDefault(Func<DDColumn, bool> predicate)
        {
            if (_columns == null) return null;

            return _columns.FirstOrDefault(predicate);
        }

        public IEnumerable<DDColumn> Where(Func<DDColumn, bool> predicate)
        {
            if (_columns == null) return Enumerable.Empty<DDColumn>();

            return _columns.Where(predicate);
        }

        public IEnumerable<T> Select<T>(Func<DDColumn, T> predicate)
        {
            if (_columns == null) return Enumerable.Empty<T>();

            return _columns.Select(predicate);
        }

        public DDColumn GetColumn(string columnName)
        {
            return GetColumn(columnName, out _);
        }

        public int GetColumnIndex(string columnName)
        {
            return _columns.FindIndex(_x => _x.ColumnName == columnName);
        }

        public string GetColumnName(int index)
        {
            if (index < 0 || index >= _columns.Count) return null;

            return _columns[index].ColumnName;
        }
        public DDColumn GetColumn(string columnName, out int index)
        {
            index = _columns.FindIndex(_x => _x.ColumnName == columnName);

            if (index == -1) return null;

            return _columns[index];
        }

        public bool TryGet(int index, out DDColumn clm)
        {
            if (!_columns.Any() || index < 0 || index >= _columns.Count)
            {
                clm = null;
                return false;
            }

            clm = _columns[index];
            return true;
        }

        public bool TryGetName(string columnName, out DDColumn clm)
        {
            clm = _columns.FirstOrDefault(x => x.ColumnName == columnName);
            return clm != null;
        }

        public DDColumn this[int index]
        {
            get
            {
                if (!_columns.Any() || index < 0 || index >= _columns.Count) return null;

                return _columns[index];
            }
        }

        public DDColumn this[string columnName]
        {
            get
            {
                return _columns.FirstOrDefault(x => x.ColumnName == columnName);
            }
        }

        public bool Any() => _columns.Any();
        public bool Any(Func<DDColumn, bool> predicate) => _columns.Any(predicate);

        public int Count => _columns.Count;

        public ColumnBehaviour ColumnBehaviour;

        public Enumerator GetEnumerator() => new Enumerator(_columns);

        public struct Enumerator
        {
            private readonly List<DDColumn> _owner;
            private int _index;

            internal Enumerator(List<DDColumn> owner)
            {
                _owner = owner;
                _index = -1;
            }

            public bool MoveNext()
            {
                int next = _index + 1;
                if ((uint)next < (uint)_owner.Count)
                {
                    _index = next;
                    return true;
                }
                return false;
            }

            // Current bir "handle" struct döner; onun üstünden MagicBox metotlarını çağırıyoruz
            public DDColumn Current => _owner[_index];

            public void Dispose() { }
        }

    }

    public class DDColumn
    {
        protected internal DDColumn(string columnName, Type columnType = null)
        {
            ColumnName = columnName;
            ColumnType = columnType;
        }

        public string ColumnName;
        public Type ColumnType;
    }

    public class DDRowCollection
    {
        private List<DDRow> _rows;
        private DDColumnCollection _columnsCollection;

        protected internal DDRowCollection(DDColumnCollection columnsCollection)
        {
            _rows = new List<DDRow>();
            _columnsCollection = columnsCollection;
        }

        public void AddRow<T>(T entity)
        {
            AddRowImpl(entity);
        }

        public void AddRowRange<T>(IEnumerable<T> entitys)
        {
            foreach (var item in entitys)
            {
                AddRowImpl(item);
            }
        }

        private void AddRowImpl<T>(T entity)
        {
            var row = new DDRow(_columnsCollection);

            if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType)
            {
                var entitydelegearry = entity.ToDelegateArray<T>(out var ColumnTypes, out var entityColumn);

                if (!_columnsCollection.Any())
                {
                    for (int i = 0; i < entityColumn.Length; i++)
                    {
                        _columnsCollection.AddColumn(entityColumn[i], ColumnTypes[i]);
                    }
                }

                for (int i = 0; i < _columnsCollection.Count; i++)
                {
                    if (!row.StrongTypeCheckOrSet(ColumnTypes[i], i, out var msgin))
                    {
                        throw new Exception(msgin);
                    }

                    row.SetDelegate(i, entitydelegearry[i]);
                }
            }
            else
            {
                row.Set(entity);
            }

            AddRow(row);
        }

        public void AddRow(DDRow row)
        {
            _rows.Add(row);
        }

        public void RemoveAt(int index)
        {
            _rows.RemoveAt(index);
        }
        public void Remove(DDRow item)
        {
            _rows.Remove(item);
        }

        public DDRow FirstOrDefault(Func<DDRow, bool> predicate)
        {
            if (_rows == null) return null;

            return _rows.FirstOrDefault(predicate);
        }

        public IEnumerable<DDRow> Where(Func<DDRow, bool> predicate)
        {
            if (_rows == null) return Enumerable.Empty<DDRow>();

            return _rows.Where(predicate);
        }

        public IEnumerable<T> Select<T>(Func<DDRow, T> predicate)
        {
            if (_rows == null) return Enumerable.Empty<T>();

            return _rows.Select(predicate);
        }

        public int Count => _rows.Count;

        public DDRow this[int index]
        {
            get
            {
                if (!_rows.Any() || index < 0 || index >= _rows.Count) return null;

                return _rows[index];
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(_rows);

        public struct Enumerator
        {
            private readonly List<DDRow> _owner;
            private int _index;

            internal Enumerator(List<DDRow> owner)
            {
                _owner = owner;
                _index = -1;
            }

            public bool MoveNext()
            {
                int next = _index + 1;
                if ((uint)next < (uint)_owner.Count)
                {
                    _index = next;
                    return true;
                }
                return false;
            }

            // Current bir "handle" struct döner; onun üstünden MagicBox metotlarını çağırıyoruz
            public DDRow Current => _owner[_index];

            public void Dispose() { }
        }
    }

    public class DDRow
    {
        private DDColumnCollection _columnsCollection;

        protected internal DDRow(DDColumnCollection columnsCollection)
        {
            _row = new DMagicBox();
            _columnsCollection = columnsCollection;
        }

        public int ColumnCount => _columnsCollection.Count;
        public int RowColumnCount => _row.Count;

        public string GetColumnName(int index) => _columnsCollection.GetColumnName(index);
        public string GetColumnName(IDBox cell)
        {
            int index = GetCellIndex(cell);

            return GetColumnName(index);
        }

        public int GetCellIndex(IDBox cell)
        {
            return _row.GetCellIndex(cell);
        }

        public bool Set<T>(T value)
        {
            return SetImpl(-1, value);
        }

        public bool Set<T>(int index, T value)
        {
            return SetImpl(index, value);
        }

        public bool Set<T>(string columnName, T value)
        {
            int index = _columnsCollection.GetColumnIndex(columnName);

            if (index == -1) return false;

            return SetImpl(index, value);
        }

        protected internal bool StrongTypeCheckOrSet<T>(int index, out string msj)
        {
            msj = "";

            if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.Free) return true;

            if (!_columnsCollection.Any())
            {
                msj = "ColumnBehaviour.StrongType da önce kolonları tanımlamanız gerekmektedir.";
                return false;
            }

            var columnType = typeof(T);

            return StrongTypeCheckOrSet(columnType, index, out msj);
        }
        protected internal bool StrongTypeCheckOrSet(Type columnType, int index, out string msj)
        {
            msj = "";

            if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.Free) return true;

            if (!_columnsCollection.Any())
            {
                msj = "ColumnBehaviour.StrongType da önce kolonları tanımlamanız gerekmektedir.";
                return false;
            }

            if (index == -1)
            {
                index = _row.Count;
            }

            if (_columnsCollection.TryGet(index, out var clm))
            {
                if (clm.ColumnType == null)
                {
                    clm.ColumnType = columnType;

                    return true;
                }

                if (columnType != clm.ColumnType)
                {
                    msj = string.Format("Tip uyumsuzluğu: Beklenen tip '{0}', ancak gelen tip '{1}'.", clm.ColumnType.FullName, columnType.FullName);
                    return false;
                }

                return true;
            }

            msj = "İstenilen kolon bulunamamıştır.";

            return false;
        }

        private bool SetImpl<T>(int index, T value)
        {
            if (index == -1 || index == _row.Count)
            {
                if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.Free
                    || (_columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && _row.Count < _columnsCollection.Count))
                {
                    if (!StrongTypeCheckOrSet<T>(index, out var msgin))
                    {
                        throw new Exception(msgin);
                    }

                    _row.Add(value);

                    return true;
                }

                return false;
            }

            if (index > _row.Count && _columnsCollection.ColumnBehaviour == ColumnBehaviour.Free
                || _columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && index > _columnsCollection.Count)
            {
                return false;
            }

            if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && index > _row.Count)
            {
                for (int i = _row.Count; i < index; i++)
                {
                    _row.AddNull();
                }

                return SetImpl<T>(index, value);
            }

            if (!StrongTypeCheckOrSet<T>(index, out var msg))
            {
                throw new Exception(msg);
            }

            return _row.Set(index, value);
        }

        public bool SetFunc<T>(Func<T> provider)
        {
            return SetFuncImpl(-1, provider);
        }

        public bool SetFunc<T>(int index, Func<T> provider)
        {
            return SetFuncImpl(index, provider);
        }

        public bool SetFunc<T>(string columnName, Func<T> provider)
        {
            int index = _columnsCollection.GetColumnIndex(columnName);

            if (index == -1) return false;

            return SetFuncImpl(index, provider);
        }

        private bool SetFuncImpl<T>(int index, Func<T> provider)
        {
            if (index == -1 || index == _row.Count)
            {
                if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.Free
                    || (_columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && _row.Count < _columnsCollection.Count))
                {
                    if (!StrongTypeCheckOrSet<T>(index, out var msgin))
                    {
                        throw new Exception(msgin);
                    }

                    _row.AddFunc(provider);

                    return true;
                }

                return false;
            }

            if (index > _row.Count && _columnsCollection.ColumnBehaviour == ColumnBehaviour.Free
                || _columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && index > _columnsCollection.Count)
            {
                return false;
            }

            if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && index > _row.Count)
            {
                for (int i = _row.Count; i < index; i++)
                {
                    _row.AddNull();
                }

                return SetFuncImpl<T>(index, provider);
            }

            if (!StrongTypeCheckOrSet<T>(index, out var msg))
            {
                throw new Exception(msg);
            }

            return _row.SetFunc(index, provider);
        }

        public bool SetDelegate(Delegate provider)
        {
            return SetDelegateImpl(-1, provider);
        }

        public bool SetDelegate(int index, Delegate provider)
        {
            return SetDelegateImpl(index, provider);
        }

        public bool SetDelegate(string columnName, Delegate provider)
        {
            int index = _columnsCollection.GetColumnIndex(columnName);

            if (index == -1) return false;

            return SetDelegateImpl(index, provider);
        }

        private bool SetDelegateImpl(int index, Delegate provider)
        {
            if (index == -1 || index == _row.Count)
            {
                if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.Free
                    || (_columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && _row.Count < _columnsCollection.Count))
                {
                    _row.AddDelegate(provider);

                    return true;
                }

                return false;
            }

            if (index > _row.Count && _columnsCollection.ColumnBehaviour == ColumnBehaviour.Free
                || _columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && index > _columnsCollection.Count)
            {
                return false;
            }

            if (_columnsCollection.ColumnBehaviour == ColumnBehaviour.StrongType && index > _row.Count)
            {
                for (int i = _row.Count; i < index; i++)
                {
                    _row.AddNull();
                }

                return SetDelegateImpl(index, provider);
            }


            return _row.SetDelegate(index, provider);
        }

        public T Get<T>(int index) => _row.Get<T>(index);

        public T Get<T>(string columnName)
        {
            var clm = _columnsCollection.GetColumn(columnName, out int columnIndex);

            return _row.Get<T>(columnIndex);
        }

        public bool TryGet<T>(int index, out T result) => _row.TryGet<T>(index, out result);

        public bool TryGet<T>(string columnName, out T result)
        {
            var clm = _columnsCollection.GetColumn(columnName, out int columnIndex);

            return _row.TryGet(columnIndex, out result);
        }

        public object GetVal(int index) => _row.GetVal(index);

        public object GetVal(string columnName)
        {
            var clm = _columnsCollection.GetColumn(columnName, out int columnIndex);

            return _row.GetVal(columnIndex);
        }

        public string GetString(int index) => _row.GetString(index);

        public string GetString(string columnName)
        {
            var clm = _columnsCollection.GetColumn(columnName, out int columnIndex);

            return _row.GetString(columnIndex);
        }

        public void Remove(IDBox item)
        {
            _row.Remove(item);
        }
        public bool Remove(string columnName)
        {
            int index = _columnsCollection.GetColumnIndex(columnName);
            return _row.RemoveAt(index);
        }
        public bool RemoveAt(int index)
        {
            return _row.RemoveAt(index);
        }

        private DMagicBox _row;

        public DMagicBox.Enumerator GetEnumerator() => _row.GetEnumerator();
    }

    public class DDBox
    {
        public DDBox(string tableName)
        {
            TableName = tableName;
            Rows = new DMagicBox();
            TableType = TableType.Box;
        }

        public string TableName { get; set; }
        public TableType TableType { get; set; }
        public int Count => Rows.GetCount();

        public IDMagicBox Rows { get; init; }
    }
}

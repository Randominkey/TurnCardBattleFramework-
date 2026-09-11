using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Utils;
using UniRx;

namespace Core.Table
{
    public interface ITable
    {
        void Load();
    }

    public class TableManager : Singleton<TableManager>
    {
        public ReactiveProperty<Language> currentLanguage { get; } = new ReactiveProperty<Language>(Language.KrString);
        private Dictionary<Type, object> tables = new Dictionary<Type, object>();

        public void Initialize()
        {
            Debug.Log("TableManager Initialized.");
        }

        public T GetTable<T>() where T : class, ITable, new()
        {
            if (tables.TryGetValue(typeof(T), out object table))
            {
                return (T)table;
            }

            T newTable = new T();
            newTable.Load();
            tables.Add(typeof(T), newTable);
            return newTable;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberRunner.Core
{
    /// <summary>
    /// Lightweight, zero-allocation-on-reuse object pool designed for 2GB-3GB RAM Android devices.
    /// Avoids runtime Instantiate/Destroy stalls across obstacles, coins, track segments, and VFX.
    /// </summary>
    public sealed class SimpleObjectPool<T> where T : Component
    {
        private readonly Stack<T> inactive;
        private readonly Func<T> factory;
        private readonly Transform poolRoot;

        public int InactiveCount => inactive.Count;

        public SimpleObjectPool(Func<T> createFunc, Transform parent, int initialCapacity = 8)
        {
            factory = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            poolRoot = parent;
            inactive = new Stack<T>(Mathf.Max(4, initialCapacity));

            Prewarm(initialCapacity);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T item = factory();
                if (poolRoot != null)
                {
                    item.transform.SetParent(poolRoot, false);
                }

                item.gameObject.SetActive(false);
                inactive.Push(item);
            }
        }

        public T Rent()
        {
            T item = inactive.Count > 0 ? inactive.Pop() : factory();
            if (poolRoot != null && item.transform.parent != poolRoot)
            {
                item.transform.SetParent(poolRoot, false);
            }

            item.gameObject.SetActive(true);
            return item;
        }

        public void Return(T item)
        {
            if (item == null)
            {
                return;
            }

            item.gameObject.SetActive(false);
            if (poolRoot != null && item.transform.parent != poolRoot)
            {
                item.transform.SetParent(poolRoot, false);
            }

            inactive.Push(item);
        }
    }
}

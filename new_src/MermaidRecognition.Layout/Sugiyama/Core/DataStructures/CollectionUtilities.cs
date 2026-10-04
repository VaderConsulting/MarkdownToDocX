using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MermaidRecognition.Layout.Sugiyama.Core.DataStructures {
    /// <summary>
    /// various utilities for collections
    /// </summary>
    public class CollectionUtilities {
        /// <summary>
        /// Add	key,value to dictionary if the key is not yet presented
        /// </summary>
        internal static void SafeAdd<T, TC>(Dictionary<T, TC> dictionary, T key, TC value) where T : notnull {
            if (!dictionary.ContainsKey(key))
                dictionary.Add(key, value);
        }

        /// <summary>
        /// Add	value to dictionary
        /// </summary>
        internal static void AddToMap<TS, T, TC>(Dictionary<T, TC> dictionary, T key, TS value)
            where T : notnull where TC : ICollection<TS>, new() {
            if (!dictionary.TryGetValue(key, out TC? tc))
                dictionary[key] = tc = new TC();

            tc.Add(value);
        }

        /// <summary>
        /// Remove value from dictionary
        /// </summary>
        internal static void RemoveFromMap<TS, T, TC>(Dictionary<T, TC> dictionary, T key, TS value)
            where T : notnull where TC : ICollection<TS> {
            TC tc = dictionary[key];
            tc.Remove(value);
            if (tc.Count == 0)
                dictionary.Remove(key);
        }
    }
}

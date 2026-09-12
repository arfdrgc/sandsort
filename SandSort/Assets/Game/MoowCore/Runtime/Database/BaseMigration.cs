using System;
using UnityEngine;

namespace Moow.DB
{
    public class BaseMigration : MonoBehaviour, IMigration, IComparable {
        #region BASE
        virtual public string migration(string json, string fileName) {
            throw new System.NotImplementedException();
        }
        #endregion

        int IComparable.CompareTo(object other) {
            string version = GetType().ToString();
            int v = int.Parse(version.Replace("V", ""));
            string otherVersion = other.GetType().ToString();
            int otherV = int.Parse(otherVersion.Replace("V", ""));

            if (v > otherV) {
                return 1;
            }

            if (v < otherV) {
                return -1;
            }

            return 0;
        }

        public override string ToString() {
            return name;
        }

        protected double ToDouble(object value) {
            return System.Convert.ToDouble(value);
        }

    }

    public interface IMigration {
        string migration(string json, string fileName);
    }
}
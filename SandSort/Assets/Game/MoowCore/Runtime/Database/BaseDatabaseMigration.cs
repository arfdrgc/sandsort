using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Moow.DB
{
    public abstract class BaseDatabaseMigration : ScriptableObject
    {
        [SerializeField] private string id;

        public string Id => $"DB_MIG_{id}";

        public abstract void Migrate(ref JObject data);

        protected string GetKey<T>()
        {
            return typeof(T).ToString();
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Moow.DB
{
    public abstract class EncryptedDatabase : SingletonDontDestroy<EncryptedDatabase>
    {
        public static string ENCRYPT_KEY = "99Games99Moow99.";
        //public static string OLD_ENCRYPT_KEY = "";

        [Header("Settings")]
        [SerializeField] protected BaseScriptableObject[] _saveData;
        [SerializeField] protected bool _resetGameAtLaunch;
        [SerializeField] protected bool _disableDBLoad;

        [Header("Migrations")]
        [SerializeField] protected List<BaseDatabaseMigration> _migrations;

        public static void SaveGame()
        {
            if (!instance)
                return;

            instance.Save();
        }

        protected override void Awake()
        {
#if UNITY_EDITOR

            Debug.Log("Persistent path: " + Application.persistentDataPath);

            if (_resetGameAtLaunch)
            {
                ResetDatabase();
            }
            else if (!_disableDBLoad)
            {
                Load();
            }
#else
            Load();
#endif
        }

        public virtual void ResetDatabase()
        {
            Debug.Log("ResetDatabase----");

            for (int i = 0; i < _saveData.Length; i++)
            {
                _saveData[i].reset();
            }

            PlayerPrefs.DeleteAll();

            Save();
        }

        public virtual void Save()
        {
            if (_saveData.Length == 0)
                return;

            var json = GetSaveDataJson();
            var path = Path.Combine(Application.persistentDataPath, "savegame.sdata");
            var encryptedJson = EncryptString(ENCRYPT_KEY, json);
            using (var streamWriter = File.CreateText(path))
            {
                streamWriter.Write(encryptedJson);
            }
        }

        public virtual void Load()
        {
            if (_saveData.Length == 0)
                return;

            var path = Path.Combine(Application.persistentDataPath, "savegame.sdata");
            if (!File.Exists(path))
            {
                ResetDatabase();
                return;
            }

            using (var streamReader = File.OpenText(path))
            {
                var encryptedJson = streamReader.ReadToEnd();
                var json = DecryptString(ENCRYPT_KEY, encryptedJson);


                //var encryptedJson = streamReader.ReadToEnd();
                //string json = null;

                //try
                //{
                //    json = DecryptString(ENCRYPT_KEY, encryptedJson);
                //}
                //catch
                //{
                //    try
                //    {
                //        Debug.Log("Trying old encryption key...");
                //        json = DecryptString(OLD_ENCRYPT_KEY, encryptedJson);

                //        // başarılıysa yeni key ile tekrar save et
                //        Debug.Log("Migrating save to new encryption key");
                //        Save();
                //    }
                //    catch
                //    {
                //        Debug.LogWarning("Save corrupted or key invalid. Resetting database.");
                //        ResetDatabase();
                //        return;
                //    }
                //}


                JObject dict;
                try
                {
                    dict = (JObject)JsonConvert.DeserializeObject(json);
                    if (dict == null)
                        return;
                }
                catch (Exception e)
                {
                    Debug.LogError("[EncryptedDatabase::Load] Error deserializing save data, couldn't load!");
                    return;
                }

                // Migrations
                foreach (var migration in _migrations)
                {
                    if (PlayerPrefs.HasKey(migration.Id))
                        continue;

                    var temp = (JObject) dict.DeepClone();
                    try
                    {
                        migration.Migrate(ref dict);
                        PlayerPrefs.SetInt(migration.Id, 1);
                    }
                    catch (Exception e)
                    {
                        dict = temp;
                        Debug.LogError($"[EncryptedDatabase::Load] Migration failed! ({e.Message})");
                    }
                }

                for (int i = 0; i < _saveData.Length; i++)
                {
                    var key = _saveData[i].GetType().ToString();
                    if (!dict.TryGetValue(key, out var jObject))
                        continue;

                    JsonUtility.FromJsonOverwrite(jObject.ToString(), _saveData[i]);
                }
            }
        }

        public string GetSaveDataJson(Formatting formatting = Formatting.None)
        {
            var dict = new Dictionary<string, BaseScriptableObject>();
            for (int i = 0; i < _saveData.Length; i++)
            {
                dict[_saveData[i].GetType().ToString()] = _saveData[i];
            }

            return JsonConvert.SerializeObject(dict, formatting);
        }

        public string EncryptString(string key, string plainText)
        {
            byte[] iv = new byte[16];
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream =
                           new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }

                        array = memoryStream.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(array);
        }

        public string DecryptString(string key, string cipherText)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = iv;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream =
                           new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
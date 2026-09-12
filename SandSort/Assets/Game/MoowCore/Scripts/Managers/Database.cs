using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Moow.DB;
using UnityEngine;

public class Database : EncryptedDatabase
{
    public static void SaveGameImmediately()
    {
        if (!instance)
            return;

        ((Database)instance).SaveImmediately();
    }

    public override void Load()
    {
        if (LoadReturningUser())
        {
            return;
        }

        base.Load();
    }

    private bool LoadReturningUser()
    {
        if (_saveData.Length == 0)
            return false;

        var isLoaded = false;
        var serializer = new BinaryFormatter();
        var path = Path.Combine(Application.persistentDataPath, "savedata");
        for (int i = 0; i < _saveData.Length; i++)
        {
            var objectToPersist = _saveData[i];
            var filePath = Path.Combine(path, $"{objectToPersist.GetType()}_{i}.sdata");
            if (!File.Exists(filePath))
                continue;

            using (var file = File.Open(filePath, FileMode.Open))
            {
                try
                {
                    var json = (string)serializer.Deserialize(file);
                    JsonUtility.FromJsonOverwrite(json, objectToPersist);
                    isLoaded = true;
                }
                catch (Exception e)
                {
                    // ignored
                }
            }

            File.Delete(filePath);
        }

        Debug.Log("isLoaded : " + isLoaded);
        return isLoaded;
    }

    protected bool _saveGame;

    private void Start()
    {
        StartCoroutine(SaveCoroutine());
    }

    public override void Save()
    {
        _saveGame = true;
    }

    public void SaveImmediately()
    {
        base.Save();
    }

    private IEnumerator SaveCoroutine()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            if (_saveGame)
            {
                base.Save();
                _saveGame = false;
            }

            yield return wait;
        }
    }
}
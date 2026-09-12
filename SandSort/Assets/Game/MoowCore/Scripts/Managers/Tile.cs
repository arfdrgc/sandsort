using System.Collections;
using System.Collections.Generic;
using Moow;
using UnityEngine;

public class Tile : MonoBehaviour {
    public Vector2Int pos { get; private set; }
    [SerializeField] private GameObject tileQuad;

    public void init(Vector2Int pos) {
        this.pos = pos;
        tileQuad.SetActive(false);
        gameObject.name = "Tile_" + pos.x + "_" + pos.y;
    }

    void OnEnable()
    {
        this.addListener<object>(Events.SET_DARK_THEME, onSetDarkTheme);
        this.addListener<object>(Events.SET_EDIT_ACTIVE, onSetEditActive);
    }

    void OnDisable()
    {
        this.removeListener<object>(Events.SET_DARK_THEME, onSetDarkTheme);
        this.removeListener<object>(Events.SET_EDIT_ACTIVE, onSetEditActive);
    }

    void onSetDarkTheme(Object sender, Event<object> eventdata)
    {
        Renderer rend = transform.GetChild(0).transform.GetComponent<Renderer>();

        // if (rend != null)
        // {
        //     rend.material = SkinManager.instance.tileDarkThemeMaterial;
        // }
    }

    void onSetEditActive(object sender, Event<object> eventdata)
    {
        #if UNITY_EDITOR
        //tileQuad.SetActive(LevelEditor.instance.isEditActive);
        #endif
    }
}

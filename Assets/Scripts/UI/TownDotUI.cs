using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TownDotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    public Image image;
    public RectTransform Rect => (RectTransform)transform;

    private WorldMapUI map;
    private int townId;
    private bool selectable;

    public Color normalColor = Color.red;
    public Color hoverColor = Color.yellow;
    public Color disabledColor = new Color(0.4f, 0.1f, 0.1f);
    public Color currentTownColor = Color.blue;

    public void Setup(int id, Vector2 nz, WorldMapUI mapUI)
    {
        townId = id;
        map = mapUI;
        image.color = normalColor;
    }

    public void SetSelectable(bool canSelect, bool isCurrent)
    {
        selectable = canSelect;

        if (isCurrent)
            image.color = currentTownColor;
        else
            image.color = selectable ? normalColor : disabledColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable)
            image.color = hoverColor;

        if (map != null)
            map.ShowTownTooltip(townId, Rect.anchoredPosition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        image.color = selectable ? normalColor : disabledColor;

        if (map != null)
            map.HideTownTooltip();
    }
    
    public void SetCurrentTown(bool isCurrent)
    {
        if (isCurrent)
            image.color = currentTownColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"CLICK town {townId}, selectable={selectable}, map={(map ? "OK" : "NULL")}");
        if (!selectable) return;
        map.OnTownClicked(townId);
    }
}
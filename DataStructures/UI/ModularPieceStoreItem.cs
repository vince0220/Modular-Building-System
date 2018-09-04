using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ModularPieceStoreItem {
	[Header("Set item settings")]
	public string Code; 
	public Sprite Image;
	public GameObject ObjectPrefab;

	[Header("Optional Settings")]
	public bool ReplaceTitle = false;
	public string Title;
	public bool OverrideCategory = false;
	public ObjectCategories Category;
}

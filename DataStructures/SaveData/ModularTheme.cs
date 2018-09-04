using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modular{
	[System.Serializable]
	public class ModularTheme {
		#region Public variables
		[Header("Theme Settings")]
		public string ThemeID = "#";

		[Header("Modular pieces")]
		public List<ModularPieceStoreSet> ThemeModularPieces = new List<ModularPieceStoreSet>();
		#endregion

		#region Private variables

		#endregion

		#region Private voids

		#endregion

		#region Public input / output voids
		public void EnsurePiecesThemeID(){
			for (int i = 0; i < ThemeModularPieces.Count; i++) {
				ThemeModularPieces [i].RegisterThemeID (ThemeID);
			}
		}
		#endregion
	}
}

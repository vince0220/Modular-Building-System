using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Modular;


namespace Modular{
	[System.Serializable]
	public class ModularPieceSaveData : ModularPlacableData {
		public string ID;
		public Color[] Colors = new Color[0];
		public string[] TextureDataKeys = new string[0];

		public ModularPieceSaveData(ModularPiece Piece): base(Piece){
			this.ID = Piece.ID;
			TextureDataKeys = Piece.CurrentTextureKeys;
			if (Piece.Colors.Length > 0) {
				Colors = new Color[Piece.Colors.Length];
				for (int i = 0; i < Piece.Colors.Length; i++) {
					Colors [i] = Piece.Colors [i].Color;
				}
			}
		}
	}
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;


namespace Modular{
	[System.Serializable]
	public class ModularSetStoreData : ModularSetData,IStoreItem {
		#region Data Variables
		public string Base64Image;
		#endregion

		#region Private variables
		private Sprite _Image;
		private string _Path;
		private int _Price;
		#endregion

		#region Constructor
		public ModularSetStoreData(ModularSet Set) : base(Set){}
		public ModularSetStoreData(ModularSet Set,Texture2D Image) : base(Set){
			this._Image = Image.ToSprite(); // convert image to sprite
			this.Base64Image = Image.ToBase64 (); // convert image to base 64
		}

		#endregion

		#region Private voids
		private void OnRemove(){

		}
		private void OnExport(){
			
		}
		#endregion

		#region Get / Set
		public string Path{
			get{
				return _Path;
			}
			set{
				_Path = value;
			}
		}
		#endregion

		public int Price {
			get {
				return _Price;
			}
			set{
				_Price = value;
			}
		}

		public string Name {
			get {
				return SetName;
			}
		}

		#region Voids
		#region Get / Setters
		public Sprite Image{
			get{
				EnsureImage (); // ensure image
				return _Image;
			}
		}
		#endregion
		#region Functions
		private void EnsureImage(){
			if(_Image == null){
				_Image = this.Base64Image.Base64ToTexture ().ToSprite(); // convert base 64 to texture
			}
		}
		#endregion
		#endregion
	}
}

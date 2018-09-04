using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modular{
	[AddComponentMenu("Modular/Interactive Piece")]
	public class InteractiveModularPiece : ModularPiece {
		public override bool DefinesBoundarys {
			get {
				return false;
			}
		}
		public override bool Scalable {
			get {
				return true;
			}
		}
	}
}

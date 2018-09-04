using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modular{
	[AddComponentMenu("Modular/Static Piece")]
	public class StaticModularPiece : ModularPiece {
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

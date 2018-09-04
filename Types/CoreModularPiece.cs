using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modular{
	[AddComponentMenu("Modular/Core Piece")]
	public class CoreModularPiece : ModularPiece, UI.UIDynamicClass {
		#region Private bools
		private bool AddedToTrack = false;
		#endregion

		#region Base voids
		protected override void OnAwake ()
		{
			base.OnAwake ();
			GetConnectionPoints ();
		}
		public override void OnPlaced ()
		{
			base.OnPlaced ();
			UpdateSurroundings (true);

			if (!AddedToTrack) {
				Management.GameManager.I.Data.AddTrackObject ("CorePieces", this.gameObject);
				AddedToTrack = true;
			}
		}
		public override void OnInitialize ()
		{
			for (int i = 0; i < ConnectionPoints.Length; i++) {
				ConnectionPoints [i].Initialize (this.gameObject.layer);
				ConnectionPoints [i].Active = true;
			}
		}
		protected override void OnModularRedo ()
		{
			if (!AddedToTrack) {
				Management.GameManager.I.Data.AddTrackObject ("CorePieces", this.gameObject);
				UpdateSurroundings (true);
				AddedToTrack = true;
			}
		}
		public override void OnDeplaced ()
		{
			base.OnDeplaced ();
			for (int i = 0; i < ConnectionPoints.Length; i++) {
				ConnectionPoints [i].Active = false;
			}
		}
		public override void Destroy ()
		{
			if (AddedToTrack) {
				Management.GameManager.I.Data.RemoveTrackObject ("CorePieces", this.gameObject);
				AddedToTrack = false;
			}
			base.Destroy ();
		}
		public override void DestoyUndo ()
		{
			if (AddedToTrack) {
				Management.GameManager.I.Data.RemoveTrackObject ("CorePieces", this.gameObject);
			}
			base.DestoyUndo ();
		}
		#endregion

		#region Private variables
		private ModularConnectionPoint[] ConnectionPoints;
		#endregion

		#region Private voids
		private void UpdateSurroundings(bool Active){
			for (int i = 0; i < ConnectionPoints.Length; i++) {
				ConnectionPoints [i].Active = Active;
				ConnectionPoints [i].OnMoved();
			}
		}
		private void GetConnectionPoints(){
			ConnectionPoints = this.GetComponentsInChildren<ModularConnectionPoint> (true);
		}
		#endregion

		#region Virtual override settings
		public override bool DefinesBoundarys {
			get {
				return true;
			}
		}
		public override bool Scalable {
			get {
				return false;
			}
		}
		public override SnapTypes DefaultSnapType {
			get {
				return SnapTypes.Center;
			}
		}
		public override RotationRestrictions RotationRestriction {
			get {
				return RotationRestrictions.Y;
			}
		}
		public override float RotationStepRestriction {
			get {
				return 90;
			}
		}
		public override SpaceRestrictions SpaceRestriction {
			get {
				return SpaceRestrictions.WorldOnly;
			}
		}
		public override PositionRestrictions PositionRestriction {
			get {
				return PositionRestrictions.GridOnly;
			}
		}
		public override bool DefaultCutGrid {
			get {
				return true;
			}
		}
		#endregion

		#region DynamicClass implementation
		public string DynamicTitle{
			get{
				return Title;
			}
		}
		#endregion
	}
}

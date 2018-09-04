using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Modular{
	[AddComponentMenu("Modular/Stroke Modular Piece")]
	public class StrokeModularPiece : ModularPiece, IStrokeConnection {
		#region Inspector variables
		[Header("Stroke Settings")]
		[HideInInspector]public StrokeType _StrokeType;
		[HideInInspector]public StrokeCategory _StrokeCategory;
		[HideInInspector]public List<ConnectionDirection> _ConnectionDirections = new List<ConnectionDirection>();
		#endregion

		#region Private variables
		private HashSet<IStrokeConnection> _CurrentConnections = new HashSet<IStrokeConnection>();
		#endregion

		#region Get / Set
		public ConnectionDirection[] WorldConnectionDirections{
			get{
				return ModularPieceExtensions.ConnectionToWorldConnection (ConnectionDirections,transform.rotation);
			}
		}
		public StrokeType StrokeType {
			get {
				return _StrokeType;
			}
		}
		public Transform ConnectionTransform{
			get{
				return this.transform;
			}
		}
		public StrokeCategory StrokeCategory {
			get {
				return _StrokeCategory;
			}
		}

		public ConnectionDirection[] ConnectionDirections {
			get {
				return _ConnectionDirections.ToArray();
			}
		}
		#endregion

		#region IStrokeConnection
		public event System.Action<IStrokeConnection[]> OnConnectionsChanged;
		public IStrokeConnection[] CurrentlyConnected {
			get {
				return _CurrentConnections.ToArray();
			}
		}
		public void OnSurroundingRemoved(IStrokeConnection Sender){
			_CurrentConnections.Remove (Sender);
			OnChangedEvent ();

		}
		public void OnSurroundingAdded(IStrokeConnection Sender){
			_CurrentConnections.Add (Sender);
			OnChangedEvent ();
		}
		#endregion

		#region public void
		public void DebugConnections(float DebugTime){
			foreach (IStrokeConnection StrokeConnection in CurrentlyConnected) {
				Debug.DrawRay (StrokeConnection.ConnectionTransform.position,Vector3.up * 5f, Color.yellow,DebugTime);
			}
		}
		public void UpdateSurroundings(){
			int LastCount = _CurrentConnections.Count;
			_CurrentConnections = this.UpdateCurrentlyConnected (
				_CurrentConnections,
				3f,
				(Position + AxisBoundsOffset).ToLocalRotationFull(transform.rotation,Position),
				Management.GameManager.I.Modular.ModularPieceTempLayer + Management.GameManager.I.Modular.ModularPieceLayer
			);
			if (_CurrentConnections.Count == 0 && LastCount == 0) {return;}
			OnChangedEvent ();
		}
		#endregion

		#region private voids
		private void RemoveSelfConnection(){
			this.RemoveSelfFromConnections (); // remove self from all connections
			_CurrentConnections = new HashSet<IStrokeConnection>();
			OnChangedEvent ();
		}
		private void OnChangedEvent(){
			if (OnConnectionsChanged != null) {OnConnectionsChanged.Invoke (CurrentlyConnected);}
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
		public override void OnPlaced ()
		{
			base.OnPlaced ();
			UpdateSurroundings (); // update surroundings
		}
		public override void OnDeplaced ()
		{
			base.OnDeplaced ();
			RemoveSelfConnection ();
		}
		public override void DestoyUndo ()
		{
			RemoveSelfConnection ();
			base.DestoyUndo ();
		}
		public override void Destroy ()
		{
			RemoveSelfConnection ();
			base.Destroy ();
		}
		protected override void OnModularUndo ()
		{
			RemoveSelfConnection ();
		}
		protected override void OnModularRedo ()
		{
			UpdateSurroundings ();
		}
		#endregion
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modular{
	public interface IStrokeConnection {
		// variables
		StrokeType StrokeType {get;}
		StrokeCategory StrokeCategory {get;}
		ConnectionDirection[] ConnectionDirections {get;}
		ConnectionDirection[] WorldConnectionDirections { get;}
		IStrokeConnection[] CurrentlyConnected{ get;}
		Transform ConnectionTransform {get;}
		event System.Action<IStrokeConnection[]> OnConnectionsChanged;

		// events
		void OnSurroundingRemoved(IStrokeConnection Sender);
		void OnSurroundingAdded (IStrokeConnection Sender);
	}
}

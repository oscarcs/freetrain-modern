using System;

namespace freetrain.world.rail
{
	/// <summary>
	/// Structures that "use" a station.
	/// 
	/// This interface is implemented by structures that have
	/// special population that uses a station. Because of the way
	/// stations find listeners, listeners need to occupy
	/// at least one voxel.
	/// 
	/// StationListener interface should be accessible through the queryAspect method.
	/// </summary>
  public interface SpecialStationListener: StationListener
	{
	}
}

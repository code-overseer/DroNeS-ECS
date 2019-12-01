using System.Collections.Generic;

namespace DroNeS.Mapbox.Custom
{
	public abstract class CustomTileFactory 
	{
		protected readonly HashSet<CustomTile> TilesWaitingResponse;
		protected readonly HashSet<CustomTile> TilesWaitingProcessing;

		protected CustomTileFactory()
		{
			TilesWaitingResponse = new HashSet<CustomTile>();
			TilesWaitingProcessing = new HashSet<CustomTile>();
		}

		public void Register(CustomTile tile)
		{
			OnRegistered(tile);
		}

		protected abstract void OnRegistered(CustomTile tile);
		
	}
}


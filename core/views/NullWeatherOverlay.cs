using System;
using System.Drawing;
using freetrain.DirectXWrapper;

namespace freetrain.views
{
	/// <summary>
	/// NullWeatherOverlay ̊Tv̐łB
	/// </summary>
	public sealed class NullWeatherOverlay : WeatherOverlay
	{
		private NullWeatherOverlay() {}

		public static readonly WeatherOverlay theInstance = new NullWeatherOverlay();

		public void setSize( Size sz ) {}

		public void draw( QuarterViewDrawer drawer, Surface target, Point pt ) {
			drawer.draw( target, pt );
		}

		public bool onTimerFired() {
			return false;
		}

		public void Dispose() {}
	}
}

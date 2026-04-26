using System;
using System.Text;

namespace freetrain.util
{
	/// <summary>
	/// Currency converter.
	/// </summary>
	public class CurrencyUtil
	{
		/// <summary>
		/// Format to a string
		/// </summary>
		public static string format( long v ) {
			string r="";  string m="";
            if( v < 0 ) m = "-";
            v = Math.Abs( v );
			while(v>=1000) {
				r = ',' + (v%1000).ToString("000") + r;
				v /= 1000;
			}
			r = m + v.ToString() + r;

			return r;
		}
	}
}

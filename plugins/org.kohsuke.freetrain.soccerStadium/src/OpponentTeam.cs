using System;

namespace freetrain.world.soccerstadium
{
	/// <summary>
	/// Opponent team.
	/// </summary>
	[Serializable]
	public class OpponentTeam
	{
		/// <summary> Name of the team. </summary>
		public readonly string name;
		/// <summary> Strength of the team. </summary>
		public readonly int strength;
		/// <summary> Popularity of the team. </summary>
		public readonly int popularity;

		private OpponentTeam( string _name, int _strength, int _popularity ) {
			this.name = _name;
			this.strength = _strength;
            this.popularity = _popularity;
		}

		public static readonly OpponentTeam[] OPPONENTS = new OpponentTeam[]{
			new OpponentTeam("ペカルタ泉台",0,10),
			new OpponentTeam("モソデティオ山岳",10,20),
			new OpponentTeam("古都ミサンガ",10,40),
			new OpponentTeam("湘北ベルマーレ",20,60),
			new OpponentTeam("ナイビレックス新形",20,30),
			new OpponentTeam("小宮アルジャジーラ",30,50),
			new OpponentTeam("ガンバレ太阪",30,50),
			new OpponentTeam("ヴィッセル榊戸",40,40),
			new OpponentTeam("横濱Fマノリス",40,70),
			new OpponentTeam("サソルッチェ狭島",40,60),
			new OpponentTeam("ジュピロ磐口",50,70),
			new OpponentTeam("ツヨイゾ太阪",50,50),
			new OpponentTeam("州崎アカンターレ",50,60),
			new OpponentTeam("鹿鳥アントラーズ",60,90),
			new OpponentTeam("名護屋クランハス",60,70),
			new OpponentTeam("FC束京",70,70),
			new OpponentTeam("浦和ブルース",90,100),
			new OpponentTeam("清氷エスプリズ",100,80)
		};


		/// <summary>
		/// Select one team randomly.
		/// </summary>
		public static OpponentTeam drawRandom() {
			return OPPONENTS[Const.rnd.Next(OPPONENTS.Length)];
		}
	}
}

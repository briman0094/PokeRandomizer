﻿namespace CtrDotNet.Pokemon.Randomization.Config
{
	public class TrainersConfig : ITrainers
	{
		public bool FriendKeepsStarter { get; set; }
		public decimal LevelMultiplier { get; set; } = 1.0m;
		public bool TypeThemed { get; set; }
	}
}
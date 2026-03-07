using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using System;

namespace AngryGrandpa
{
	/// <summary>The class for patching methods on the StardewValley.Utility class.</summary>
	public class UtilityPatches
	{
		/*********
        ** Accessors
        *********/
		private static IModHelper Helper => ModEntry.Instance.Helper;
		private static IMonitor Monitor => ModEntry.Instance.Monitor;
		private static ModConfig Config => ModConfig.Instance;
		private static Harmony Harmony => ModEntry.Instance.Harmony;


		/*********
        ** Public methods
        *********/
		/// <summary>
		/// Applies the harmony patches defined in this class.
		/// </summary>
		public static void Apply()
		{
			Harmony.Patch(
				original: AccessTools.Method(typeof(Utility),
					nameof(Utility.getGrandpaScore)),
				postfix: new HarmonyMethod(typeof(UtilityPatches),
					nameof(UtilityPatches.getGrandpaScore_Postfix))
			); 
			Harmony.Patch(
				original: AccessTools.Method(typeof(Utility),
					nameof(Utility.getGrandpaCandlesFromScore)),
				postfix: new HarmonyMethod(typeof(UtilityPatches),
					nameof(UtilityPatches.getGrandpaCandlesFromScore_Postfix))
			);
		}

		/// <summary>
		/// Alters the points scoring for grandpa's evaluation if config ScoringSystem: "Original"
		/// </summary>
		/// <param name="__result">The original result returned by Utility.getGrandpaScore</param>
		/// <returns>Integer result of raw points score</returns>
		public static int getGrandpaScore_Postfix(int __result)
		{
			try
			{
				// Only modify scoring if they wanted the original 13 points
				if (Config.ScoringSystem == "Original") 
				{
					int score = 0;
					if (Game1.player.totalMoneyEarned >= 100000U) // Earned 100K+
						++score;
					if (Game1.player.totalMoneyEarned >= 200000U) // Earned 200K+
						++score;
					if (Game1.player.totalMoneyEarned >= 300000U) // Earned 300K+
						++score;
					if (Game1.player.totalMoneyEarned >= 500000U) // Earned 500K+
						++score;
					if (Game1.player.totalMoneyEarned >= 1000000U) // Earned 1 million +
						++score;
					if (Utility.foundAllStardrops()) // Found all stardrops(!!!) Very difficult.
						++score;
					if (Game1.isLocationAccessible("CommunityCenter")) // All CC bundles are complete
						++score;
					if (Game1.player.isMarriedOrRoommates() && Utility.getHomeOfFarmer(Game1.player).upgradeLevel >= 2) // Married with 2nd house upgrade
						++score;
					if (Game1.player.achievements.Contains(5)) // A Complete Collection (museum)
						++score;
					if (Game1.player.achievements.Contains(26)) // Master Angler (catch every fish)
						++score;
					if (Game1.player.achievements.Contains(34)) // Full Shipment (ship every item)
						++score;
					if (Utility.getNumberOfFriendsWithinThisRange(Game1.player, 1975, 999999, false) >= 10) // 8 hearts with 10+ villagers
						++score;
					if (Game1.player.Level >= 25) // Total 50 levels in skills (max all)
						++score;
					return score; // return revised score
				}
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(getGrandpaScore_Postfix)}:\n{ex}",
					LogLevel.Error);
			}
			return __result; // Return original calculated score
		}

		/// <summary>
		/// Alters the conversion of raw points score to candle number depending on a user's config.
		/// </summary>
		/// <param name="__result">The original result returned by Utility.getGrandpaCandlesFromScore</param>
		/// <param name="score">The raw points score out of 21 (or 13 if ScoringSystem: "Original")</param>
		/// <returns>Number of grandpa candles from 1-4</returns>
		public static int getGrandpaCandlesFromScore_Postfix(int __result, int score)
		{
			try
			{
				if (score >= Config.GetScoreForCandles(4))
					return 4;
				if (score >= Config.GetScoreForCandles(3))
					return 3;
				return score >= Config.GetScoreForCandles(2) ? 2 : 1;
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(getGrandpaCandlesFromScore_Postfix)}:\n{ex}",
					LogLevel.Error);
			}
			return __result;
		}
	}
}
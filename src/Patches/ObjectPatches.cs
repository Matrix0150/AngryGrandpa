using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using Object = StardewValley.Object;
using Netcode;
using System;

namespace AngryGrandpa
{
	/// <summary>The class for patching methods on the StardewValley.Object class.</summary>
	public class ObjectPatches
	{
		/*********
        ** Accessors
        *********/
		private static IModHelper Helper => ModEntry.Instance.Helper;
		private static IMonitor Monitor => ModEntry.Instance.Monitor;
		private static ModConfig Config => ModConfig.Instance;
		private static Harmony Harmony => ModEntry.Instance.Harmony;


		/*********
        ** Fields
        *********/
		protected static ITranslationHelper i18n = Helper.Translation;


		/*********
        ** Public methods
        *********/
		/// <summary>
		/// Applies the harmony patches defined in this class.
		/// </summary>
		public static void Apply()
		{
			Harmony.Patch(
				original: AccessTools.Method(typeof(Object),
					nameof(Object.checkForSpecialItemHoldUpMeessage)), // Yes, the game code spells it Meessage. -_-
				postfix: new HarmonyMethod(typeof(ObjectPatches),
					nameof(ObjectPatches.checkForSpecialItemHoldUpMeessage_Postfix))
			);
		}

		/// <summary>
		/// Provides a special message to display for the bonus reward items given by grandpa's shrine.
		/// In theory this *could* be triggered by non-shrine actions, but it's unlikely under normal circumstances.
		/// </summary>
		/// <param name="__result">The original string result returned by checkForSpecialItemHoldUpMeessage</param>
		/// <param name="__instance">The SDV Object that the method was called on.</param>
		public static void checkForSpecialItemHoldUpMeessage_Postfix(ref string __result, Object __instance)
		{
			try
			{
				if (!__instance.bigCraftable.Value &&
				    Game1.getFarm().grandpaScore.Value != 0 &&
					Game1.currentLocation is Farm)
				{
					switch (__instance.QualifiedItemId)
					{
						case "(O)114": // Ancient seed
							__result = i18n.Get("Object.cs.1CandleReward");
							break;
						case "(O)107": // Dinosaur egg
							__result = i18n.Get("Object.cs.2CandleReward");
							break;
						case "(O)74": // Prismatic shard
							__result = i18n.Get("Object.cs.3CandleReward");
							break;
					}
				}
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(checkForSpecialItemHoldUpMeessage_Postfix)}:\n{ex}",
					LogLevel.Error);
			}
		}
	}
}
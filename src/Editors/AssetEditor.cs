using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Math;

namespace AngryGrandpa
{
	internal class AssetEditor
	{
		/*********
        ** Accessors
        *********/
		private static IModHelper Helper => ModEntry.Instance.Helper;
		private static IMonitor Monitor => ModEntry.Instance.Monitor;
		private static ModConfig Config => ModConfig.Instance;
		private static ITranslationHelper i18n => Helper.Translation;


		/*********
        ** Constants
        *********/

		// Asset names
		private const string Locations    = "Strings\\Locations";
		private const string Mail            = "Data\\mail";
		private const string GrandpaPortraits    = "Portraits\\Grandpa";
		private const string Code  = "Strings\\StringsFromCSFiles";
		private const string FarmEvent          = "Data\\Events\\Farm";
		private const string FarmhouseEvent     = "Data\\Events\\Farmhouse";

		// Portrait asset paths
		private const string PortraitPathDefault      = "assets\\Grandpa.png";
		private const string PortraitPathPoltergeister = "assets\\Poltergeister\\Grandpa.png";

		// Compiled Regex patterns
		private static readonly Regex RegexCandleEventKey     = new Regex(@"^2146991\/.*",   RegexOptions.Compiled);
		private static readonly Regex RegexEvaluationEventKey = new Regex(@"^55829(1|2)\/.*", RegexOptions.Compiled);
		private static readonly Regex RegexYearPrecondition   = new Regex(@"/y [0-9]+",       RegexOptions.Compiled);

		/// <summary>i18n data keys (base) for the evaluation and re-evaluation dialogue scripts.</summary>
		private static readonly List<string> EvaluationStrings = 
		[
			"1CandleResult",
			"2CandleResult",
			"3CandleResult",
			"4CandleResult",
			"1CandleReevaluation",
			"2CandleReevaluation",
			"3CandleReevaluation",
			"4CandleReevaluation",
		];
				
		public void Register()
		{
			Helper.Events.Content.AssetRequested += OnAssetRequested;
		}
		
		private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
		{
			// --- GrandpaNoteEditor ---
			if (e.NameWithoutLocale.IsEquivalentTo(Locations) ||
				e.NameWithoutLocale.IsEquivalentTo(Mail))
			{
				e.Edit(EditGrandpaNote);
			}

			// --- PortraitEditor ---
			// When PortraitStyle is "auto" (default), apply early so other mods can override.
			// When PortraitStyle is explicitly set, apply late to take priority over other mods.
			if (e.NameWithoutLocale.IsEquivalentTo(GrandpaPortraits))
			{
				bool isAutoStyle = Config.PortraitStyle == ModConfig.PortraitStyleDefault;
				if (isAutoStyle)
				{
					e.Edit(EditPortraitEarly, AssetEditPriority.Early);
				}
				else
				{
					e.Edit(EditPortraitOverride, AssetEditPriority.Late);
				}
			}

			// --- EvaluationEditor ---
			if (e.NameWithoutLocale.IsEquivalentTo(Code))
			{
				e.Edit(EditEvaluation);
			}

			// --- EventEditor ---
			if (e.NameWithoutLocale.IsEquivalentTo(FarmEvent) ||
				e.NameWithoutLocale.IsEquivalentTo(FarmhouseEvent))
			{
				e.Edit(EditEvents);
			}
		}


		// =====================================================================
		// GrandpaNoteEditor
		// =====================================================================

		/// <summary>Edit the Strings\Locations grandpa note entry and the Data\mail entry.</summary>
		private void EditGrandpaNote(IAssetData asset)
		{
			const string GameKeyLocations = "Farm_GrandpaNote";
			const string GameKeyMail      = "6324grandpaNoteMail";

			string gameKey;
			string modKey;
			string value;

			if (asset.NameWithoutLocale.IsEquivalentTo(Locations))
			{
				gameKey = GameKeyLocations;
				modKey = "GrandpaNote";
			}
			else if (asset.NameWithoutLocale.IsEquivalentTo(Mail))
			{
				gameKey = GameKeyMail;
				modKey = "GrandpaNoteMail";
			}
			else { return; }

			if (Config.YearsBeforeEvaluation >= 10)
			{
				modKey += "TenPlusYears";
				string smapiSDate = new SDate(1, "spring", Config.YearsBeforeEvaluation + 1).ToLocaleString();
				value = i18n.Get(modKey, new { smapiSDate });
			}
			else
			{
				string ordinalYear = i18n.Get("GrandpaOrdinalYears").ToString().Split('|')[Config.YearsBeforeEvaluation];
				value = i18n.Get(modKey, new { ordinalYear });
			}

			var data = asset.AsDictionary<string, string>().Data;
			data[gameKey] = value;
		}


		// =====================================================================
		// PortraitEditor
		// =====================================================================

		/// <summary>Early portrait edit — may be overridden by Content Patcher mods.</summary>
		private void EditPortraitEarly(IAssetData asset)
		{
			const string ModSlightlyEditedPortraits = "Poltergeister.SlightlyEditedPortraits";
			const string ModSeasonalCuteCharacters  = "Poltergeister.SeasonalCuteCharacters";

			string filepath = PortraitPathDefault;

			if (Helper.ModRegistry.IsLoaded(ModSlightlyEditedPortraits) ||
				Helper.ModRegistry.IsLoaded(ModSeasonalCuteCharacters))
			{
				filepath = PortraitPathPoltergeister;
				Monitor.LogOnce("Compatible portrait mod found! Loading Poltergeister-style expressive portraits to match.", LogLevel.Info);
			}
			else
			{
				Monitor.LogOnce("Loading default Vanilla-style expressive portraits. Other mods that change the grandpa portrait may be able to overwrite these images.", LogLevel.Info);
			}

			ApplyPortraitPatch(asset, filepath);
		}

		/// <summary>Late portrait edit — overrides Content Patcher and other mods.</summary>
		private void EditPortraitOverride(IAssetData asset)
		{
			string filepath = PortraitPathDefault;

			if (Config.PortraitStyle == "Poltergeister")
			{
				filepath = PortraitPathPoltergeister;
				Monitor.LogOnce("Loading Poltergeister-style expressive portraits. This will take priority over other mods that change grandpa's portrait.", LogLevel.Info);
			}
			else if (Config.PortraitStyle == "Vanilla")
			{
				Monitor.LogOnce("Loading Vanilla-style expressive portraits. This will take priority over other mods that change grandpa's portrait.", LogLevel.Info);
			}

			ApplyPortraitPatch(asset, filepath);
		}

		/// <summary>Load the portrait texture and patch it onto the asset, with fallback to the default asset.</summary>
		private void ApplyPortraitPatch(IAssetData asset, string filepath)
		{
			Texture2D sourceImage;
			try
			{
				sourceImage = Helper.ModContent.Load<Texture2D>(filepath);
			}
			catch
			{
				Monitor.LogOnce($"Loading grandpa portrait asset at {filepath} failed. Reverting to default mod asset.", LogLevel.Warn);
				sourceImage = Helper.ModContent.Load<Texture2D>(PortraitPathDefault);
			}

			var editor = asset.AsImage();
			editor.ExtendImage(minWidth: 128, minHeight: 384);
			editor.PatchImage(sourceImage);
		}


		// =====================================================================
		// EvaluationEditor
		// =====================================================================

		/// <summary>Edit evaluation entries in Strings\StringsFromCSFiles with new dialogues and tokens.</summary>
		private void EditEvaluation(IAssetData asset)
		{
			// Requires an active game for spouse, NPC, and year info
			if (!Context.IsWorldReady)
				return;

			// Prepare tokens
			string pastYears;
			int yearsPassed = Max(Game1.year - 1, Config.YearsBeforeEvaluation);

			if (yearsPassed >= 10)
			{
				pastYears = Config.GrandpaDialogue == "Nuclear"
					? i18n.Get("GrandpaDuringManyYears.Nuclear")
					: i18n.Get("GrandpaDuringManyYears");
			}
			else
			{
				pastYears = i18n.Get("GrandpaDuringPastYears").ToString().Split('|')[yearsPassed];
			}

			string spouseOrLewis = Game1.player.isMarriedOrRoommates()
				? "%spouse"
				: Game1.getCharacterFromName<NPC>("Lewis").displayName;

			string fifthCandle = "";
			bool inOneYear = Game1.year == 1 || (Game1.year == 2 && Game1.currentSeason == "spring" && Game1.dayOfMonth == 1);
			if (Utility.getGrandpaScore() >= 21 && inOneYear)
			{
				fifthCandle = i18n.Get("FifthCandle." + Config.GrandpaDialogue);
			}

			var allEvaluationTokens = new Dictionary<string, string>(Config.PortraitTokens)
			{
				["pastYears"] = pastYears,
				["spouseOrLewis"] = spouseOrLewis,
				["fifthCandle"] = fifthCandle
			};

			var data = asset.AsDictionary<string, string>().Data;

			foreach (string entry in EvaluationStrings)
			{
				string gameKey = i18n.Get(entry + ".gameKey");
				string modKey = entry + "." + Config.GrandpaDialogue;
				if (Config.GenderNeutrality) { modKey += "-gn"; }
				string value = i18n.Get(modKey, allEvaluationTokens);

				data[gameKey] = value;
			}
		}


		// =====================================================================
		// EventEditor
		// =====================================================================

		/// <summary>Edit the Farmhouse evaluation events and Farm candle event keys and scripts.</summary>
		private void EditEvents(IAssetData asset)
		{
			var data = asset.AsDictionary<string, string>().Data;

			// --- Farm: fix CandleEvent year precondition ---
			if (asset.NameWithoutLocale.IsEquivalentTo(FarmEvent))
			{
				string entry = "CandleEvent";
				string value = i18n.Get(entry + ".gameValue");
				string gameKey = i18n.Get(entry + ".gameKey");

				// Find and remove any existing CandleEvent key (starts with "2146991/")
				List<string> toDelete = data.Keys.Where(k => RegexCandleEventKey.IsMatch(k)).ToList();
				foreach (string k in toDelete)
				{
					value = data[k]; // Preserve existing event script
					data.Remove(k);
				}

				// Rewrite the year precondition
				gameKey = Config.YearsBeforeEvaluation > 0
					? RegexYearPrecondition.Replace(gameKey, $"/y {Config.YearsBeforeEvaluation + 1}")
					: RegexYearPrecondition.Replace(gameKey, "");

				data[gameKey] = value;
			}

			// --- Farmhouse: fix EvaluationEvent / RepeatEvaluationEvent ---
			else if (asset.NameWithoutLocale.IsEquivalentTo(FarmhouseEvent))
			{
				string countYears;
				int yearsPassed = Max(Game1.year - 1, Config.YearsBeforeEvaluation);

				if (yearsPassed >= 10)
				{
					countYears = Config.GrandpaDialogue == "Nuclear"
						? i18n.Get("GrandpaCountManyYears.Nuclear")
						: i18n.Get("GrandpaCountManyYears");
				}
				else
				{
					countYears = i18n.Get("GrandpaCountYears").ToString().Split('|')[yearsPassed];
				}

				var allEventTokens = new Dictionary<string, string>(Config.PortraitTokens)
				{
					["countYears"] = countYears
				};

				// Remove old evaluation event keys (start with "558291/" or "558292/")
				List<string> toDelete = data.Keys.Where(k => RegexEvaluationEventKey.IsMatch(k)).ToList();
				foreach (string k in toDelete)
				{
					data.Remove(k);
					Monitor.Log($"Removed event key: {k}", LogLevel.Trace);
				}

				// Insert corrected keys and scripts
				foreach (string entry in new List<string> { "EvaluationEvent", "RepeatEvaluationEvent" })
				{
					string gameKey = i18n.Get(entry + ".gameKey");
					string modKey = entry + "." + Config.GrandpaDialogue;
					if (Config.GenderNeutrality) { modKey += "-gn"; }

					gameKey = Config.YearsBeforeEvaluation > 0
						? RegexYearPrecondition.Replace(gameKey, $"/y {Config.YearsBeforeEvaluation + 1}")
						: RegexYearPrecondition.Replace(gameKey, "");

					Monitor.Log($"New event key for {entry}: {gameKey}", LogLevel.Trace);
					data[gameKey] = i18n.Get(modKey, allEventTokens);
				}
			}
		}
	}
}
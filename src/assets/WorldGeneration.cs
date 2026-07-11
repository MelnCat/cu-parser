using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class WorldGeneration : MonoBehaviour
{
	public delegate bool PlaceCheckDelegate(Vector2Int pos);

	public enum OverrideSceneType
	{
		None = 0,
		Tutorial = 1,
		Debug = 2
	}

	public float maxTimePerLayer;

	public uint width;

	public uint height;

	public uint chunkWidth;

	public uint chunkHeight;

	private ushort[,] worldBlocks;

	private Tilemap[,] chunks;

	[HideInInspector]
	public TilemapRenderer[,] renderChunks;

	private ChunkScript[,] chunkScripts;

	private GameObject square;

	public TileBase[] tiles;

	public Color[] tileColors;

	public string[] biomeTitles;

	public static int CHUNKSIZE = 64;

	public Grid worldGrid;

	public static WorldGeneration world;

	public Tile specialNullTile;

	private Camera mainCam;

	private Vector3 lastCameraPos;

	[HideInInspector]
	public bool generatingWorld;

	public Material defaultMat;

	public Material glowMat;

	public float ambientTemperature = 24f;

	public float temperatureOffset;

	public UnityEvent[,] ChunkUpdated;

	public List<BlockDamage> blockDamages = new List<BlockDamage>();

	public Sprite[] blockDamageSprites;

	public Gradient structureDamageGrad;

	private bool instantiatingWorld;

	public int totalTraveled;

	public int biomeDepth;

	public GameObject loadingObject;

	public TextMeshProUGUI loadingText;

	public float lootRarityMultiplier = 1f;

	public float trapRarityMultiplier = 1f;

	public GameObject blockBreakPrefab;

	public AnimationCurve[] temperatureCurves;

	public AudioClip[] backgroundDrones;

	public int currentTempCurve;

	public float currentCurveProgress;

	public AudioSource caveAudio;

	public float timeSinceFinishedGeneration;

	public TextMeshProUGUI biomeTitle;

	public string[] possibleFootSteps;

	public Dictionary<string, AudioClip[]> footstepDict;

	public Gradient skyColors;

	private Color skyColor;

	public Material skyMaterial;

	public VolumeProfile[] biomeProfiles;

	private static float[] biomeProfileNoise;

	public VolumeProfile tutorialProfile;

	public Material fogMat;

	public SpriteRenderer fogSprite;

	public float fogAmount;

	public OverrideSceneType biomeOverride;

	public float genTimePassed;

	public RectTransform[] genRects;

	public AudioSource genPodSource;

	public AudioMixerGroup soundMixerGroup;

	public bool unchippedMode;

	public bool lineOfSight;

	public int debugStartDepth;

	public Texture2D iceMap;

	public AnimationCurve iceGenCurve;

	public AnimationCurve iceMinGenCurve;

	private bool doingRegen;

	public bool doPod;

	public Texture2D fungalRainMap;

	public float fungusRainIntensity;

	public SpriteRenderer rainSprite;

	public AudioSource rainLoopSource;

	public AudioSource earthquakeSource;

	public float earthquakeIntensity;

	public float earthquakeTime;

	public float earthquakeDelay;

	public string[] spawnableMagazines;

	public GameObject savePanel;

	public bool forceRain;

	private string layerPrefix;

	private string layerDescription;

	public float layerTimeSpent;

	[HideInInspector]
	public int amountOfLayers;

	public float realTimeElapsed;

	public static Dictionary<string, object> runSettings;

	public static float globalDecayRate = 1f;

	public Light2D ambientLight;

	public List<GameObject> backgrounds = new List<GameObject>();

	private float bonusTemperatureOffset;

	public bool worldExists
	{
		get
		{
			if (chunks != null && chunks.Length > 0)
			{
				return !instantiatingWorld;
			}
			return false;
		}
	}

	public uint halfWidth => (uint)((float)width * 0.5f);

	public uint halfHeight => (uint)((float)height * 0.5f);

	public int HALFCHUNKSIZE => (int)((float)CHUNKSIZE * 0.5f);

	public float totalLootRarity => GetRunSettingFloat("baselootdensity") * lootRarityMultiplier;

	public float totalTrapRarity => GetRunSettingFloat("basetrapdensity") * trapRarityMultiplier;

	public static bool unchipped => world.unchippedMode;

	public static bool lineOfSightEnabled
	{
		get
		{
			if (!world.lineOfSight)
			{
				return world.unchippedMode;
			}
			return true;
		}
	}

	private Body body => PlayerCamera.main.body;

	private void Awake()
	{
		world = this;
		if (runSettings == null)
		{
			runSettings = RunSettings.GetPreset("normal").presetValues;
		}
		ConsoleScript.CheckForConsole();
		GlobalDark.CheckForDark();
		if (PlayerPrefs.GetInt("tutorial") > 0)
		{
			PlayerPrefs.SetInt("tutorial", 0);
			biomeOverride = OverrideSceneType.Tutorial;
		}
		caveAudio = GetComponent<AudioSource>();
		Item.SetupItems();
		Recipes.SetUpRecipes();
		timeSinceFinishedGeneration = 1000f;
		footstepDict = new Dictionary<string, AudioClip[]>();
		amountOfLayers = temperatureCurves.Length;
		amountOfLayers = 5;
		string[] array = possibleFootSteps;
		foreach (string text in array)
		{
			footstepDict.Add(text, Resources.LoadAll<AudioClip>("Sounds/footstep/" + text + "/"));
		}
	}

	public static float GetRunSettingFloat(string name)
	{
		return (float)runSettings[name];
	}

	public static bool GetRunSettingBool(string name)
	{
		return (bool)runSettings[name];
	}

	public static int GetRunSettingInt(string name)
	{
		return (int)runSettings[name];
	}

	private void OnDestroy()
	{
		worldBlocks = null;
	}

	public void SetUnchipped(bool unchipped)
	{
		unchippedMode = unchipped;
		PlayerCamera.main.SetUnchippedUI(unchipped);
		if (!unchippedMode)
		{
			PlayerCamera.main.LOSHole.GetComponent<VisionMask>().ClearMask();
		}
		else if ((bool)WoundView.view && WoundView.view.armorMode)
		{
			WoundView.view.ToggleMode();
		}
	}

	public static float TotalRunTime()
	{
		return SaveSystem.savedRunTime + Time.timeSinceLevelLoad;
	}

	public float PlayerLayerDepthMeters()
	{
		return (0f - (body.transform.position.y - (float)halfHeight)) * 0.3f;
	}

	public float PlayerLayerDepthUnits()
	{
		return 0f - (body.transform.position.y - (float)halfHeight);
	}

	public float RadlineLayerDepthMeters()
	{
		return (0f - (RadiationLine.line.transform.position.y - (float)halfHeight)) * 0.3f;
	}

	public float RadlineLayerDepthUnits()
	{
		return 0f - (RadiationLine.line.transform.position.y - (float)halfHeight);
	}

	public float PlayerTotalDepthUnits()
	{
		return PlayerLayerDepthUnits() + (float)totalTraveled / 0.3f;
	}

	public float PlayerTotalDepthMeters()
	{
		return PlayerLayerDepthMeters() + (float)totalTraveled;
	}

	public float RadlineTotalDepthMeters()
	{
		return RadlineLayerDepthMeters() + (float)totalTraveled;
	}

	public string TotalDepthString()
	{
		int num = Mathf.RoundToInt(PlayerTotalDepthMeters());
		return $"{num}m";
	}

	public string RadlineTotalDepthString()
	{
		int num = Mathf.RoundToInt(RadlineTotalDepthMeters());
		return $"{num}m";
	}

	private void Start()
	{
		earthquakeDelay = UnityEngine.Random.Range(240f, 1000f);
		skyColor = skyColors.Evaluate(UnityEngine.Random.value);
		skyMaterial.SetColor("_TopColor", skyColor);
		skyMaterial.SetFloat("_RainIntensity", (UnityEngine.Random.Range(0f, 1f) < 0.3f) ? 1f : 0f);
		square = Resources.Load("Square") as GameObject;
		mainCam = Camera.main;
		biomeDepth = debugStartDepth;
		for (int i = 0; i < biomeTitles.Length; i++)
		{
			biomeTitles[i] = Locale.GetOther("layertitle" + (i + 1));
		}
		SaveSystem.TryLoadGame();
		if (GetRunSettingBool("unchipped"))
		{
			SetUnchipped(unchipped: true);
		}
		trapRarityMultiplier += GetRunSettingFloat("trapincrease") * (float)debugStartDepth;
		maxTimePerLayer = GetRunSettingFloat("timelimit") * 60f;
		globalDecayRate = GetRunSettingFloat("itemdecayrate");
		bonusTemperatureOffset = GetRunSettingFloat("temperatureoffset");
		FluidManager.main.liquidPushing = GetRunSettingBool("liquidpushing");
		if (GetRunSettingBool("debugworld"))
		{
			chunkHeight = 4u;
			chunkWidth = 4u;
		}
		else
		{
			chunkWidth = 16u;
			chunkHeight = 16u;
		}
		if (biomeProfileNoise == null)
		{
			biomeProfileNoise = new float[biomeProfiles.Length];
			for (int j = 0; j < biomeProfiles.Length; j++)
			{
				if (biomeProfiles[j].TryGet<FilmGrain>(out var component))
				{
					biomeProfileNoise[j] = component.intensity.value;
				}
				else
				{
					biomeProfileNoise[j] = 0f;
				}
			}
		}
		StartCoroutine("InstantiateWorld", true);
	}

	public void CreateHitFlash(Sprite sprite, Vector2 pos, Quaternion rot, Color clr, Transform follow = null)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load("Special/HitFlash"), pos, rot) as GameObject;
		if ((bool)follow)
		{
			gameObject.transform.SetParent(follow);
		}
		gameObject.transform.localScale = Vector3.one;
		gameObject.GetComponent<SpriteRenderer>().sprite = sprite;
		gameObject.GetComponent<HitFlash>().clr = clr;
	}

	public AudioClip RandomStepSound(string step)
	{
		return footstepDict[step][UnityEngine.Random.Range(0, footstepDict[step].Length)];
	}

	public BlockInfo GetBlockInfo(ushort block)
	{
		return block switch
		{
			0 => new BlockInfo
			{
				name = Locale.GetOther("air"),
				health = 0f,
				stepsound = "Rock",
				sleep = Body.SleepQuality.Okay
			}, 
			1 => new BlockInfo
			{
				name = Locale.GetOther("lightrock"),
				health = 100f,
				hitsound = "rock",
				stepsound = "Rock",
				sleep = Body.SleepQuality.Bad
			}, 
			2 => new BlockInfo
			{
				name = Locale.GetOther("gravel"),
				health = 25f,
				hitsound = "dirt",
				stepsound = "Gravel",
				sleep = Body.SleepQuality.Okay
			}, 
			3 => new BlockInfo
			{
				name = Locale.GetOther("scrappile"),
				health = 60f,
				hitsound = "scrapmetal",
				stepsound = "Scrap",
				sleep = Body.SleepQuality.Mediocre
			}, 
			4 => new BlockInfo
			{
				name = Locale.GetOther("trashpile"),
				health = 20f,
				hitsound = "trash",
				stepsound = "Scrap",
				sleep = Body.SleepQuality.Mediocre
			}, 
			5 => new BlockInfo
			{
				name = Locale.GetOther("concretetile"),
				health = 800f,
				hitsound = "concrete",
				stepsound = "Concrete",
				sleep = Body.SleepQuality.Mediocre
			}, 
			6 => new BlockInfo
			{
				name = Locale.GetOther("steeltile"),
				health = 5000f,
				hitsound = "steel",
				stepsound = "Steel",
				metallic = true,
				sleep = Body.SleepQuality.Mediocre
			}, 
			7 => new BlockInfo
			{
				name = Locale.GetOther("glass"),
				health = 30f,
				hitsound = "glass",
				stepsound = "Glass",
				noVariation = true,
				sleep = Body.SleepQuality.Mediocre
			}, 
			8 => new BlockInfo
			{
				name = Locale.GetOther("rubber"),
				health = 60f,
				hitsound = "rubber",
				stepsound = "Rubber",
				sleep = Body.SleepQuality.Good
			}, 
			9 => new BlockInfo
			{
				name = Locale.GetOther("plastic"),
				health = 150f,
				hitsound = "rubber",
				stepsound = "Plastic",
				sleep = Body.SleepQuality.Okay
			}, 
			10 => new BlockInfo
			{
				name = Locale.GetOther("heatresistantalloy"),
				health = 15000f,
				hitsound = "steel",
				stepsound = "Steel",
				metallic = true,
				sleep = Body.SleepQuality.Mediocre
			}, 
			11 => new BlockInfo
			{
				name = Locale.GetOther("wood"),
				health = 150f,
				hitsound = "wood",
				stepsound = "Wood",
				noVariation = true,
				sleep = Body.SleepQuality.Okay
			}, 
			12 => new BlockInfo
			{
				name = Locale.GetOther("sand"),
				health = 15f,
				hitsound = "sand",
				stepsound = "Sand",
				sleep = Body.SleepQuality.Good
			}, 
			13 => new BlockInfo
			{
				name = Locale.GetOther("sandstone"),
				health = 90f,
				hitsound = "rock",
				stepsound = "Rock",
				sleep = Body.SleepQuality.Bad
			}, 
			14 => new BlockInfo
			{
				name = Locale.GetOther("infinirock"),
				health = 420133760f,
				hitsound = "rock",
				stepsound = "Rock",
				sleep = Body.SleepQuality.Bad
			}, 
			15 => new BlockInfo
			{
				name = Locale.GetOther("clay"),
				health = 25f,
				hitsound = "sand",
				stepsound = "Sand",
				sleep = Body.SleepQuality.Okay
			}, 
			16 => new BlockInfo
			{
				name = Locale.GetOther("soil"),
				health = 32f,
				hitsound = "dirt",
				stepsound = "Gravel",
				sleep = Body.SleepQuality.Okay
			}, 
			17 => new BlockInfo
			{
				name = Locale.GetOther("granite"),
				health = 200f,
				hitsound = "rock",
				stepsound = "Concrete",
				sleep = Body.SleepQuality.Bad
			}, 
			18 => new BlockInfo
			{
				name = Locale.GetOther("marble"),
				health = 150f,
				hitsound = "rock",
				stepsound = "Concrete",
				sleep = Body.SleepQuality.Bad
			}, 
			19 => new BlockInfo
			{
				name = Locale.GetOther("limestone"),
				health = 135f,
				hitsound = "rock",
				stepsound = "Concrete",
				sleep = Body.SleepQuality.Bad
			}, 
			20 => new BlockInfo
			{
				name = Locale.GetOther("bricks"),
				health = 650f,
				hitsound = "concrete",
				stepsound = "Concrete",
				noVariation = true,
				sleep = Body.SleepQuality.Bad
			}, 
			21 => new BlockInfo
			{
				name = Locale.GetOther("scaffolding"),
				health = 200f,
				hitsound = "steel",
				stepsound = "Steel",
				noVariation = true,
				metallic = true,
				sleep = Body.SleepQuality.Mediocre
			}, 
			22 => new BlockInfo
			{
				name = Locale.GetOther("toxirock"),
				health = 250f,
				hitsound = "rock",
				stepsound = "Concrete",
				toxicity = 2.5f,
				sleep = Body.SleepQuality.Bad
			}, 
			23 => new BlockInfo
			{
				name = Locale.GetOther("grass"),
				health = 35f,
				hitsound = "rustle",
				stepsound = "Grass",
				sleep = Body.SleepQuality.Good
			}, 
			24 => new BlockInfo
			{
				name = Locale.GetOther("log"),
				health = 150f,
				hitsound = "wood",
				stepsound = "Wood",
				sleep = Body.SleepQuality.Mediocre,
				noVariation = true
			}, 
			25 => new BlockInfo
			{
				name = Locale.GetOther("leaves"),
				health = 20f,
				hitsound = "rustle",
				stepsound = "Grass",
				sleep = Body.SleepQuality.Okay
			}, 
			26 => new BlockInfo
			{
				name = Locale.GetOther("snow"),
				health = 15f,
				hitsound = "sand",
				stepsound = "Snow",
				sleep = Body.SleepQuality.Good
			}, 
			27 => new BlockInfo
			{
				name = Locale.GetOther("ice"),
				health = 50f,
				hitsound = "glass",
				stepsound = "Ice",
				sleep = Body.SleepQuality.Mediocre,
				slippery = true
			}, 
			28 => new BlockInfo
			{
				name = Locale.GetOther("thinice"),
				health = 1f,
				hitsound = "glass",
				stepsound = "Ice",
				sleep = Body.SleepQuality.Mediocre,
				slippery = true
			}, 
			29 => new BlockInfo
			{
				name = Locale.GetOther("powdersnow"),
				health = 1f,
				hitsound = "sand",
				stepsound = "Snow",
				sleep = Body.SleepQuality.Good
			}, 
			30 => new BlockInfo
			{
				name = Locale.GetOther("heavyrock"),
				health = 200f,
				hitsound = "rock",
				stepsound = "Rock",
				sleep = Body.SleepQuality.Bad
			}, 
			31 => new BlockInfo
			{
				name = Locale.GetOther("fungus"),
				health = 50f,
				hitsound = "gore2",
				stepsound = "Grass",
				sleep = Body.SleepQuality.Okay
			}, 
			32 => new BlockInfo
			{
				name = Locale.GetOther("mushroombody"),
				health = 80f,
				hitsound = "gore2",
				stepsound = "Plastic",
				sleep = Body.SleepQuality.Mediocre
			}, 
			33 => new BlockInfo
			{
				name = Locale.GetOther("mushroomcap"),
				health = 60f,
				hitsound = "gore2",
				stepsound = "Plastic",
				sleep = Body.SleepQuality.Mediocre
			}, 
			34 => new BlockInfo
			{
				name = Locale.GetOther("copper"),
				health = 2000f,
				hitsound = "crystal",
				stepsound = "Rock",
				sleep = Body.SleepQuality.Bad
			}, 
			35 => new BlockInfo
			{
				name = Locale.GetOther("ilmenite"),
				health = 4000f,
				hitsound = "rock",
				stepsound = "Rock",
				sleep = Body.SleepQuality.Bad
			}, 
			_ => null, 
		};
	}

	public Vector2Int WorldToBlockPos(Vector2 pos)
	{
		return new Vector2Int((int)(pos.x + (float)halfWidth), (int)(pos.y + (float)halfHeight));
	}

	public void SetBlock(Vector2Int pos, ushort block)
	{
		pos = new Vector2Int((int)Mathf.Clamp(pos.x, 0f, width - 1), (int)Mathf.Clamp(pos.y, 0f, height - 1));
		worldBlocks[pos.x, pos.y] = block;
		UpdateChunkClosest(pos);
	}

	public void SetBlockNoUpdate(Vector2Int pos, ushort block)
	{
		worldBlocks[(int)Mathf.Clamp(pos.x, 0f, width - 2), (int)Mathf.Clamp(pos.y, 0f, height - 2)] = block;
	}

	private void UpdateTile(Vector2Int pos)
	{
		Vector3Int position = new Vector3Int(pos.x % CHUNKSIZE, pos.y % CHUNKSIZE);
		GetClosestChunk(pos).SetTile(position, tiles[GetBlock(pos)]);
	}

	public ushort GetBlock(Vector2Int pos)
	{
		return worldBlocks[(int)Math.Clamp(pos.x, 0L, width - 1), (int)Math.Clamp(pos.y, 0L, height - 1)];
	}

	public ushort GetBlock(Vector2 pos)
	{
		return GetBlock(WorldToBlockPos(pos));
	}

	public BlockDamage GetBlockDamage(Vector2Int pos)
	{
		return blockDamages.Where((BlockDamage x) => x.pos == pos).FirstOrDefault();
	}

	public void ClearBlockDamages()
	{
		foreach (BlockDamage blockDamage in blockDamages)
		{
			blockDamage.DestroySprite();
		}
		blockDamages.Clear();
	}

	public void DamageBlock(Vector2Int pos, float dmg, bool hitSound = true, bool bonusMetal = false, bool ignoreLoot = false)
	{
		BlockDamage blockDamage = null;
		BlockInfo blockInfo = GetBlockInfo(GetBlock(pos));
		dmg *= ((bonusMetal && blockInfo.metallic) ? 10f : 1f);
		if (blockDamages.Count > 0)
		{
			blockDamage = GetBlockDamage(pos);
		}
		if (blockDamage != null)
		{
			blockDamage.damage += dmg;
		}
		else
		{
			blockDamage = new BlockDamage
			{
				pos = pos,
				damage = dmg
			};
			blockDamages.Add(blockDamage);
			if (blockDamages.Count > 128)
			{
				blockDamages[0].damage = 1E+10f;
				blockDamages[0].UpdateSprite();
				blockDamages.RemoveAt(0);
			}
		}
		blockDamage.UpdateSprite();
		if (blockDamage.damage >= blockInfo.health)
		{
			Sound.Play(GetBlockInfo(GetBlock(pos)).hitsound, BlockToWorldPos(pos));
			Sound.Play(RandomStepSound(GetBlockInfo(GetBlock(pos)).stepsound), BlockToWorldPos(pos));
			Sprite sprite = GetClosestChunk(pos).GetSprite(new Vector3Int(pos.x % CHUNKSIZE - HALFCHUNKSIZE, pos.y % CHUNKSIZE - HALFCHUNKSIZE));
			if ((bool)sprite)
			{
				GameObject obj = UnityEngine.Object.Instantiate(blockBreakPrefab, BlockToWorldPos(pos), Quaternion.identity);
				ParticleSystem.ShapeModule shape = obj.GetComponent<ParticleSystem>().shape;
				shape.texture = sprite.texture;
				obj.GetComponent<ParticleSystem>().Play();
			}
			if (!ignoreLoot)
			{
				switch (GetBlock(pos))
				{
				case 7:
					if (UnityEngine.Random.value < 0.4f)
					{
						Utils.Create("glassshards", BlockToWorldPos(pos), 0f);
					}
					break;
				case 3:
					if (UnityEngine.Random.value < 0.5f)
					{
						Utils.Create("scrapmetal", BlockToWorldPos(pos), 0f).GetComponent<Item>().condition = UnityEngine.Random.Range(0.05f, 0.2f);
					}
					break;
				case 6:
					if (UnityEngine.Random.value < 0.75f)
					{
						Utils.Create("scrapmetal", BlockToWorldPos(pos), 0f).GetComponent<Item>().condition = UnityEngine.Random.Range(0.5f, 1f);
					}
					break;
				case 10:
					if (UnityEngine.Random.value < 0.75f)
					{
						Utils.Create("scrapmetal", BlockToWorldPos(pos), 0f).GetComponent<Item>().condition = UnityEngine.Random.Range(0.5f, 1f);
					}
					break;
				case 27:
					if (UnityEngine.Random.value < 0.5f)
					{
						FluidManager.main.fluid[pos.x, pos.y] = 1;
					}
					break;
				case 8:
				case 9:
					if (UnityEngine.Random.value < 0.5f)
					{
						Utils.Create("plasticchunk", BlockToWorldPos(pos), 0f);
					}
					break;
				case 11:
				case 24:
					switch (UnityEngine.Random.Range(0, 3))
					{
					case 0:
						Utils.Create("woodscraps", BlockToWorldPos(pos), 0f);
						break;
					case 1:
						Utils.Create("stick", BlockToWorldPos(pos), 0f);
						break;
					case 2:
						Utils.Create("woodpanel", BlockToWorldPos(pos), 0f);
						break;
					}
					break;
				case 34:
					Utils.Create("rawcopper", BlockToWorldPos(pos), 0f);
					break;
				case 35:
					Utils.Create("ilmenitechunk", BlockToWorldPos(pos), 0f);
					break;
				}
			}
			SetBlock(pos, 0);
			UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DustBig"), BlockToWorldPos(pos), Quaternion.identity);
			blockDamages.Remove(blockDamage);
		}
		else if (hitSound)
		{
			Sound.Play(GetBlockInfo(GetBlock(pos)).hitsound, BlockToWorldPos(pos));
		}
	}

	public void DamageBlock(Vector2 pos, float dmg, bool hitSound = true, bool bonusMetal = false)
	{
		DamageBlock(WorldToBlockPos(pos), dmg, hitSound, bonusMetal);
	}

	private void Update()
	{
		realTimeElapsed += Time.unscaledDeltaTime;
		layerTimeSpent += Time.deltaTime;
		if (layerTimeSpent > maxTimePerLayer && !RadiationLine.line.active && biomeOverride == OverrideSceneType.None)
		{
			RadiationLine.line.Activate();
		}
		earthquakeDelay -= Time.deltaTime;
		if (earthquakeDelay < 0f && biomeOverride == OverrideSceneType.None && !body.sleeping)
		{
			earthquakeDelay = UnityEngine.Random.Range(600f, 1750f) * GetRunSettingFloat("timebetweenearthquakes");
			earthquakeTime = UnityEngine.Random.Range(3f, 25f);
			Time.timeScale = 1f;
		}
		earthquakeTime -= Time.deltaTime;
		earthquakeIntensity = Mathf.MoveTowards(earthquakeIntensity, (earthquakeTime > 0f) ? 1f : 0f, Time.deltaTime * 0.1f);
		if (earthquakeIntensity > 0f)
		{
			if (UnityEngine.Random.value / Time.deltaTime < 8f)
			{
				PlayerCamera.main.shaker.Shake(earthquakeIntensity * 20f);
				if (PlayerCamera.main.body.standing)
				{
					PlayerCamera.main.body.rb.velocity += UnityEngine.Random.insideUnitCircle * earthquakeIntensity * 10f * (PlayerCamera.main.body.grounded ? 2.5f : 1f);
				}
				else
				{
					Vector2 vector = UnityEngine.Random.insideUnitCircle * earthquakeIntensity * 10f;
					Limb[] limbs = PlayerCamera.main.body.limbs;
					for (int i = 0; i < limbs.Length; i++)
					{
						limbs[i].rb.velocity += vector;
					}
				}
			}
			if (UnityEngine.Random.value / Time.deltaTime < 16f * earthquakeIntensity)
			{
				SetBlock(WorldToBlockPos((Vector2)PlayerCamera.main.body.transform.position + UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(5f, 30f)), 0);
			}
			if (earthquakeIntensity > 0.5f)
			{
				PlayerCamera.main.body.eyeScareTime = 1f;
			}
		}
		earthquakeSource.volume = earthquakeIntensity;
		fungusRainIntensity = 0f;
		if (biomeDepth == 6 || forceRain)
		{
			Vector2Int vector2Int = WorldToBlockPos(PlayerCamera.main.body.transform.position);
			Vector2 vector2 = new Vector2((float)vector2Int.x / (float)width, (float)vector2Int.y / (float)height);
			fungusRainIntensity = fungalRainMap.GetPixelBilinear(vector2.x, vector2.y).r;
			if (PlayerCamera.main.body.wetness < fungusRainIntensity * 100f)
			{
				PlayerCamera.main.body.wetness = Mathf.MoveTowards(PlayerCamera.main.body.wetness, fungusRainIntensity * 100f, Time.deltaTime);
			}
			PlayerCamera.main.body.dirtyness = Mathf.MoveTowards(PlayerCamera.main.body.dirtyness, 0f, Time.deltaTime * fungusRainIntensity);
			if (UnityEngine.Random.value < Time.deltaTime * fungusRainIntensity && PlayerCamera.main.dropletAmount <= 0.05f)
			{
				PlayerCamera.main.SetDroplets(Color.white);
			}
		}
		rainSprite.color = new Color(1f, 1f, 1f, fungusRainIntensity);
		rainLoopSource.volume = fungusRainIntensity;
		Time.fixedDeltaTime = ((Time.timeScale >= 5f && PlayerCamera.main.blackAmount >= 1f) ? 0.05f : 0.02f);
		timeSinceFinishedGeneration += Time.deltaTime;
		ambientTemperature = temperatureCurves[currentTempCurve].Evaluate(Time.timeSinceLevelLoad) + temperatureOffset + bonusTemperatureOffset;
		if (!generatingWorld && worldBlocks != null && Input.GetKey(KeyCode.Mouse0))
		{
			_ = (Vector2)mainCam.ScreenToWorldPoint(Input.mousePosition);
		}
		if (chunks != null && Vector3.SqrMagnitude(mainCam.transform.position - lastCameraPos) > (float)HALFCHUNKSIZE)
		{
			lastCameraPos = mainCam.transform.position;
			if (worldExists)
			{
				UpdateChunkVisibility();
			}
		}
		if (generatingWorld || instantiatingWorld)
		{
			bool flag = biomeDepth == 0 && biomeOverride == OverrideSceneType.None;
			if (flag)
			{
				genTimePassed += Time.unscaledDeltaTime;
				Vector2 anchoredPosition = new Vector2(UnityEngine.Random.Range(-9f, 0f), 0f);
				genRects[1].anchoredPosition = anchoredPosition;
				genRects[2].anchoredPosition = anchoredPosition;
				genRects[4].anchoredPosition = new Vector2(0f, UnityEngine.Random.Range((0f - genRects[4].sizeDelta.y) * 0.4f, genRects[4].sizeDelta.y * 0.4f));
				if (!genPodSource.isPlaying)
				{
					genPodSource.Play();
				}
			}
			for (int j = 0; j < genRects.Length; j++)
			{
				if (j != 2 && j != 0)
				{
					genRects[j].GetComponent<Image>().enabled = flag;
				}
			}
			genRects[0].GetComponent<Image>().color = (flag ? Color.white : Color.black);
			genRects[5].GetComponent<Image>().enabled = !flag;
		}
		else
		{
			genTimePassed = 0f;
			if (genPodSource.isPlaying)
			{
				genPodSource.Stop();
			}
		}
		if (biomeDepth == 5 && PlayerCamera.main.body.temperature < 32.5f)
		{
			PlayerCamera.main.body.snowAmount = Mathf.MoveTowards(PlayerCamera.main.body.snowAmount, 1f, Time.deltaTime * 0.0125f);
		}
		else
		{
			PlayerCamera.main.body.snowAmount = Mathf.MoveTowards(PlayerCamera.main.body.snowAmount, 0f, Time.deltaTime * 0.02f);
		}
		if (!savePanel.activeSelf && !doingRegen && !generatingWorld && worldExists && PlayerCamera.main.body.transform.position.y < (float)(0L - (long)halfHeight) + 3.1f && biomeOverride != OverrideSceneType.Tutorial)
		{
			savePanel.SetActive(value: true);
			body.forceWalk = true;
		}
		UpdateAmbientLight();
	}

	private void UpdateAmbientLight()
	{
		float intensity = 0f;
		switch (GetRunSettingInt("ambientlight"))
		{
		case 1:
			intensity = 0.12f;
			break;
		case 2:
			intensity = 0.4f;
			break;
		}
		if (ConsoleScript.instance.fullBright || body.bothEyesGone)
		{
			intensity = 0.7f;
		}
		ambientLight.intensity = intensity;
	}

	public void ContinueRun()
	{
		if (!doingRegen && !generatingWorld && worldExists && PlayerCamera.main.body.transform.position.y < (float)(0L - (long)halfHeight) + 3.1f)
		{
			body.forceWalk = false;
			savePanel.SetActive(value: false);
			PlayerPrefs.SetInt("deepestlayer", Math.Max(biomeDepth + 1, PlayerPrefs.GetInt("deepestlayer")));
			StartCoroutine(RegenerateWorld());
		}
	}

	public void SaveAndExit()
	{
		body.forceWalk = false;
		PlayerPrefs.SetInt("deepestlayer", Math.Max(biomeDepth + 1, PlayerPrefs.GetInt("deepestlayer")));
		IncreaseDepthByLayer();
		SaveSystem.SaveGame();
		PlayerCamera.main.ToMainMenu();
	}

	private IEnumerator ReloadScene()
	{
		yield return Clear();
		Time.timeScale = 1f;
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

	public IEnumerator RegenerateWorld(bool twice = false)
	{
		doingRegen = true;
		GlobalDark.main.Darken();
		yield return new WaitUntil(() => !GlobalDark.main.IsDarkening());
		IncreaseDepthByLayer();
		if (twice)
		{
			IncreaseDepthByLayer();
		}
		if (biomeDepth < amountOfLayers - 1)
		{
			biomeDepth += ((!twice) ? 1 : 2);
		}
		else
		{
			biomeDepth = 1;
		}
		PlayerPrefs.SetInt("deepestlayer", Math.Max(biomeDepth, PlayerPrefs.GetInt("deepestlayer")));
		lootRarityMultiplier *= GetRunSettingFloat("lootmultiplier");
		trapRarityMultiplier += GetRunSettingFloat("trapincrease");
		yield return Clear();
		doingRegen = false;
		yield return InstantiateWorld(generate: true);
	}

	public void IncreaseDepthByLayer()
	{
		totalTraveled += (int)((float)height * 0.3f);
		body.skills.AddExp(2, 80f);
	}

	public IEnumerator Clear()
	{
		loadingObject.SetActive(value: true);
		SetLoadingText("genclearingworld");
		generatingWorld = true;
		yield return null;
		BuildingEntity[] array = UnityEngine.Object.FindObjectsOfType<BuildingEntity>();
		for (int i = 0; i < array.Length; i++)
		{
			UnityEngine.Object.Destroy(array[i].gameObject);
		}
		foreach (Item allItem in Item.allItems)
		{
			if (allItem.transform.parent == null || allItem.transform.parent.name == "DOSPAWN")
			{
				UnityEngine.Object.Destroy(allItem.gameObject);
			}
		}
		yield return null;
		backgrounds.Clear();
		foreach (Transform item in worldGrid.transform)
		{
			UnityEngine.Object.Destroy(item.gameObject);
		}
		generatingWorld = false;
		chunks = null;
	}

	public void UpdateWorld()
	{
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				chunks[i / CHUNKSIZE, j / CHUNKSIZE].SetTile(new Vector3Int(i % CHUNKSIZE - HALFCHUNKSIZE, j % CHUNKSIZE - HALFCHUNKSIZE), tiles[worldBlocks[i, j]]);
			}
		}
	}

	public void RandomizeTileTransforms()
	{
		ulong num = (ulong)UnityEngine.Random.Range(0, int.MaxValue);
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				num = lehmer64(num);
				num = lehmer64(num);
				chunks[i / CHUNKSIZE, j / CHUNKSIZE].SetTransformMatrix(new Vector3Int(i % CHUNKSIZE - HALFCHUNKSIZE, j % CHUNKSIZE - HALFCHUNKSIZE), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, num % 5 * 90), new Vector3(1f, 1f, 1f)));
			}
		}
	}

	public static ulong lehmer64(ulong state)
	{
		state *= 15750249268501108917uL;
		return state;
	}

	public Tilemap GetClosestChunk(Vector2Int pos)
	{
		return chunks[(int)checked((nint)unchecked(Math.Clamp(pos.x / CHUNKSIZE, 0L, chunkWidth - 1))), (int)checked((nint)unchecked(Math.Clamp(pos.y / CHUNKSIZE, 0L, chunkHeight - 1)))];
	}

	public ChunkScript GetClosestChunkScript(Vector2Int pos)
	{
		return chunkScripts[(int)checked((nint)unchecked(Math.Clamp(pos.x / CHUNKSIZE, 0L, chunkWidth - 1))), (int)checked((nint)unchecked(Math.Clamp(pos.y / CHUNKSIZE, 0L, chunkHeight - 1)))];
	}

	public ChunkScript GetClosestChunkScript(Vector2 poss)
	{
		Vector2Int vector2Int = WorldToBlockPos(poss);
		return chunkScripts[(int)checked((nint)unchecked(Math.Clamp(vector2Int.x / CHUNKSIZE, 0L, chunkWidth - 1))), (int)checked((nint)unchecked(Math.Clamp(vector2Int.y / CHUNKSIZE, 0L, chunkHeight - 1)))];
	}

	public TilemapRenderer GetClosestChunkRenderer(Vector2Int pos)
	{
		return renderChunks[(int)checked((nint)unchecked(Math.Clamp(pos.x / CHUNKSIZE, 0L, chunkWidth - 1))), (int)checked((nint)unchecked(Math.Clamp(pos.y / CHUNKSIZE, 0L, chunkHeight - 1)))];
	}

	public Vector2Int BlockToChunkPos(Vector2Int pos)
	{
		return new Vector2Int(Math.Clamp(pos.x / CHUNKSIZE, 0, (int)(chunkWidth - 1)), Math.Clamp(pos.y / CHUNKSIZE, 0, (int)(chunkHeight - 1)));
	}

	public void UpdateChunkClosest(Vector2Int pos)
	{
		UpdateChunk(new Vector2Int(pos.x / CHUNKSIZE, pos.y / CHUNKSIZE));
	}

	public void UpdateChunk(Vector2Int chunk)
	{
		for (int i = 0; i < CHUNKSIZE; i++)
		{
			for (int j = 0; j < CHUNKSIZE; j++)
			{
				chunks[chunk.x, chunk.y].SetTile(new Vector3Int(i - HALFCHUNKSIZE, j - HALFCHUNKSIZE), tiles[worldBlocks[i + chunk.x * CHUNKSIZE, j + chunk.y * CHUNKSIZE]]);
			}
		}
		ChunkUpdated[chunk.x, chunk.y].Invoke();
	}

	public void UpdateChunkVisibility()
	{
		if (generatingWorld)
		{
			return;
		}
		int num = (int)mainCam.transform.position.x / CHUNKSIZE - 3 + (int)((float)chunkWidth * 0.5f);
		int num2 = (int)mainCam.transform.position.x / CHUNKSIZE + 4 + (int)((float)chunkWidth * 0.5f);
		int num3 = (int)mainCam.transform.position.y / CHUNKSIZE - 3 + (int)((float)chunkHeight * 0.5f);
		int num4 = (int)mainCam.transform.position.y / CHUNKSIZE + 4 + (int)((float)chunkHeight * 0.5f);
		for (int i = num; i < num2; i++)
		{
			for (int j = num3; j < num4; j++)
			{
				if (i >= 0 && j >= 0 && i < chunkWidth && j < chunkHeight)
				{
					if (i == num || i == num2 - 1 || j == num3 || j == num4 - 1)
					{
						renderChunks[i, j].gameObject.GetComponent<TilemapRenderer>().enabled = false;
					}
					else
					{
						renderChunks[i, j].gameObject.GetComponent<TilemapRenderer>().enabled = true;
					}
				}
			}
		}
	}

	public void DisableAllChunks()
	{
		Tilemap[,] array = chunks;
		int upperBound = array.GetUpperBound(0);
		int upperBound2 = array.GetUpperBound(1);
		for (int i = array.GetLowerBound(0); i <= upperBound; i++)
		{
			for (int j = array.GetLowerBound(1); j <= upperBound2; j++)
			{
				array[i, j].GetComponent<TilemapRenderer>().enabled = false;
			}
		}
	}

	public void GenerateObjectAtPos(Vector2Int pos, Tilemap tilemap, float blockChance = 1f, bool genMode = false)
	{
		for (int i = tilemap.cellBounds.xMin; i < tilemap.cellBounds.xMax; i++)
		{
			for (int j = tilemap.cellBounds.yMin; j < tilemap.cellBounds.yMax; j++)
			{
				if (!tilemap.HasTile(new Vector3Int(i, j)) || !(UnityEngine.Random.Range(0f, 1f) < blockChance))
				{
					continue;
				}
				try
				{
					if (tilemap.GetTile(new Vector3Int(i, j)) == specialNullTile)
					{
						if (genMode)
						{
							worldBlocks[pos.x + i, pos.y + j] = 0;
						}
						else
						{
							SetBlock(new Vector2Int(pos.x + i, pos.y + j), 0);
						}
					}
					else if (genMode)
					{
						worldBlocks[pos.x + i, pos.y + j] = (ushort)Array.IndexOf(tiles, tilemap.GetTile(new Vector3Int(i, j)));
					}
					else
					{
						SetBlock(new Vector2Int(pos.x + i, pos.y + j), (ushort)Array.IndexOf(tiles, tilemap.GetTile(new Vector3Int(i, j))));
					}
				}
				catch
				{
				}
			}
		}
	}

	public void GenerateObjectAtPosFast(Vector2Int pos, Tilemap tilemap)
	{
		Dictionary<TileBase, ushort> dictionary = new Dictionary<TileBase, ushort>();
		for (int i = 0; i < tiles.Length; i++)
		{
			dictionary.Add(tiles[i] ?? specialNullTile, (ushort)i);
		}
		for (int j = tilemap.cellBounds.xMin; j < tilemap.cellBounds.xMax; j++)
		{
			for (int k = tilemap.cellBounds.yMin; k < tilemap.cellBounds.yMax; k++)
			{
				if (tilemap.GetTile(new Vector3Int(j, k)) == specialNullTile)
				{
					worldBlocks[pos.x + j, pos.y + k] = 0;
				}
				else
				{
					worldBlocks[pos.x + j, pos.y + k] = dictionary[tilemap.GetTile(new Vector3Int(j, k)) ?? specialNullTile];
				}
			}
		}
	}

	public void GenerateEntityAtPos(Vector2 pos, GameObject basObj)
	{
		pos -= Vector2.one * 0.5f;
		foreach (Transform item in basObj.transform)
		{
			if ((bool)item.GetComponent<Item>() || (bool)item.GetComponent<BuildingEntity>() || item.gameObject.name == "DOSPAWN")
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(item.gameObject, pos + (Vector2)item.localPosition, item.localRotation);
				if ((bool)gameObject.GetComponent<Tilemap>())
				{
					gameObject.transform.SetParent(worldGrid.transform);
				}
			}
		}
	}

	public void PlaceCrystals()
	{
		string[] array = new string[7] { "BloodCrystal", "SoothingCrystal", "ReliefCrystal", "TurbulentCrystal", "OxygenCrystal", "EmissiveCrystal", "DigestionCrystal" };
		for (int i = 0; i < 5; i++)
		{
			DistributeEntities((GameObject)Resources.Load(array[UnityEngine.Random.Range(0, array.Length)]), 0.015f, 0.015f, 2f, 0f, 0f, spawnInGround: false, randomFlip: true);
		}
	}

	public void DistributeEntities(GameObject basObj, float minPerChunk, float maxPerChunk, float spawnYOffset = 0f, float randomRotation = 0f, float spawnYOffsetDeviation = 0f, bool spawnInGround = false, bool randomFlip = false, PlaceCheckDelegate checkFunc = null, bool isTrap = false, Vector2 dir = default(Vector2), bool forceFlip = false)
	{
		if (dir == Vector2.zero)
		{
			dir = Vector2.down;
		}
		float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(minPerChunk, maxPerChunk);
		for (int i = 0; (float)i < num; i++)
		{
			Vector2 vector = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, isTrap ? (body.transform.position.y - 5f) : ((float)halfHeight)));
			if (!(!Physics2D.OverlapPoint(vector, LayerMask.GetMask("Ground")) || spawnInGround))
			{
				continue;
			}
			RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, dir, CHUNKSIZE, LayerMask.GetMask("Ground"));
			if (!raycastHit2D || !(MathF.Abs(raycastHit2D.point.x) < (float)halfWidth - 1f) || !(MathF.Abs(raycastHit2D.point.y) < (float)halfHeight - 1f) || (checkFunc != null && !checkFunc(WorldToBlockPos(raycastHit2D.point - Vector2.up * 0.5f))))
			{
				continue;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(basObj, raycastHit2D.point - dir * UnityEngine.Random.Range(spawnYOffset - spawnYOffsetDeviation, spawnYOffset + spawnYOffsetDeviation), Quaternion.Euler(0f, 0f, basObj.transform.eulerAngles.z + UnityEngine.Random.Range(0f - randomRotation, randomRotation)));
			if ((bool)gameObject.GetComponent<BuildingEntity>())
			{
				gameObject.GetComponent<BuildingEntity>().blockPlacedOn = WorldToBlockPos(raycastHit2D.point + dir * 0.5f);
				if (gameObject.GetComponent<BuildingEntity>().requireGround)
				{
					ChunkUpdated[WorldToBlockPos(raycastHit2D.point + dir * 0.5f).x / CHUNKSIZE, WorldToBlockPos(raycastHit2D.point + dir * 0.5f).y / CHUNKSIZE].AddListener(gameObject.GetComponent<BuildingEntity>().CheckSeating);
				}
			}
			if (randomFlip)
			{
				gameObject.transform.localScale = new Vector3((UnityEngine.Random.Range(0f, 1f) > 0.5f) ? (-1f) : 1f, 1f, 1f);
			}
			if (forceFlip)
			{
				gameObject.transform.localScale = new Vector3(-1f, 1f, 1f);
			}
		}
	}

	public void GenerateBlockCircle(Vector2 pos, int size, ushort block, float chance, float chanceEnd, bool autoUpdateChunk = false, bool force = false, bool ignoreInfinirock = false)
	{
		for (int i = 0; i < size * 2; i++)
		{
			for (int j = 0; j < size * 2; j++)
			{
				float num = Vector2.Distance(pos + Vector2.up * j + Vector2.right * i - Vector2.one * size, pos);
				if (!(num < (float)size) || !(UnityEngine.Random.Range(0f, 1f) < Mathf.Lerp(chance, chanceEnd, num / (float)size)))
				{
					continue;
				}
				Vector2Int pos2 = WorldToBlockPos(pos);
				pos2.x += -size + i;
				pos2.y += -size + j;
				if (pos2.x < 0)
				{
					pos2.x = 0;
				}
				if (pos2.x > width - 1)
				{
					pos2.x = (int)(width - 1);
				}
				if (pos2.y < 0)
				{
					pos2.y = 0;
				}
				if (pos2.y > height - 1)
				{
					pos2.y = (int)(height - 1);
				}
				if ((worldBlocks[pos2.x, pos2.y] > 0 && (!ignoreInfinirock || worldBlocks[pos2.x, pos2.y] != 14)) || force)
				{
					if (autoUpdateChunk)
					{
						SetBlock(pos2, block);
					}
					else
					{
						worldBlocks[pos2.x, pos2.y] = block;
					}
				}
			}
		}
	}

	public void SimpleBlockCircle(Vector2Int pos, int size, ushort block)
	{
		for (int i = 0; i < size * 2; i++)
		{
			for (int j = 0; j < size * 2; j++)
			{
				if (Vector2.Distance(pos + Vector2.up * j + Vector2.right * i - Vector2.one * size, pos) < (float)size)
				{
					Vector2Int vector2Int = pos;
					vector2Int.x += -size + i;
					vector2Int.y += -size + j;
					if (vector2Int.x < 0)
					{
						vector2Int.x = 0;
					}
					if (vector2Int.x > width - 1)
					{
						vector2Int.x = (int)(width - 1);
					}
					if (vector2Int.y < 0)
					{
						vector2Int.y = 0;
					}
					if (vector2Int.y > height - 1)
					{
						vector2Int.y = (int)(height - 1);
					}
					worldBlocks[vector2Int.x, vector2Int.y] = block;
				}
			}
		}
	}

	public bool isSoil(Vector2Int pos)
	{
		int block = GetBlock(pos);
		if (block != 2 && block != 15 && block != 16 && block != 23)
		{
			if (block > 30)
			{
				return block < 34;
			}
			return false;
		}
		return true;
	}

	public void SetLoadingTextNoLocale(string text)
	{
		loadingText.text = text;
	}

	public void CreateBackground(string path, Tilemap chunk, int sortOffset = 0, bool glow = false)
	{
		GameObject gameObject = new GameObject("ChunkBack", typeof(SpriteRenderer));
		gameObject.transform.SetParent(chunk.transform);
		gameObject.transform.localPosition = Vector2.zero;
		gameObject.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>(path);
		gameObject.GetComponent<SpriteRenderer>().material = (glow ? glowMat : defaultMat);
		gameObject.GetComponent<SpriteRenderer>().drawMode = SpriteDrawMode.Tiled;
		gameObject.GetComponent<SpriteRenderer>().size = Vector2.one * CHUNKSIZE;
		gameObject.GetComponent<SpriteRenderer>().sortingOrder = -9999 + sortOffset;
		gameObject.GetComponent<SpriteRenderer>().color = GetBackgroundColor();
		backgrounds.Add(gameObject);
	}

	public void UpdateAllBackgroundColors()
	{
		foreach (GameObject background in backgrounds)
		{
			background.GetComponent<SpriteRenderer>().color = GetBackgroundColor();
		}
	}

	public Color GetBackgroundColor()
	{
		if (!Settings.Get<SettingBool>("darkerbackground").value)
		{
			return Color.gray;
		}
		return new Color(0.2f, 0.2f, 0.2f);
	}

	public void PlaceLiquids(float perChunk, byte type, int maxFill)
	{
		float num = (float)(chunkWidth * chunkHeight) * perChunk;
		for (int i = 0; (float)i < num; i++)
		{
			Vector2 pos = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight - 32));
			FluidManager.main.StartFill(WorldToBlockPos(pos), type, maxFill);
		}
	}

	public void SetLoadingText(string localetext)
	{
		loadingText.text = Locale.GetOther(localetext);
	}

	private IEnumerator GenerateWorld()
	{
		loadingObject.SetActive(value: true);
		generatingWorld = true;
		yield return WorldPreprocess();
		yield return WorldCreateBackground();
		yield return WorldGenerateTerrain();
		yield return WorldGenerateWorldBorders();
		SetLoadingText("gencreatingcolliders");
		yield return null;
		UpdateWorld();
		yield return WorldPlacePlayer();
		yield return WorldPlaceEntities();
		yield return FinishWorldGeneration();
	}

	private IEnumerator WorldPlaceEntities()
	{
		SetLoadingText("genplacingentities");
		yield return null;
		if (biomeDepth <= 1 && biomeOverride == OverrideSceneType.None)
		{
			PlaceCrystals();
			DistributeEntities((GameObject)Resources.Load("glowplant"), 2.7f, 3.5f, 1.25f, 10f, 0.25f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("stoneplant"), 0.4f, 0.5f, 1.9f, 10f, 0.1f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("ceilingrye"), 0.3f, 0.4f, 1f, 10f, 0.5f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3), isTrap: false, Vector2.up);
			DistributeEntities((GameObject)Resources.Load("medcrate"), 0.18f * totalLootRarity, 0.2f * totalLootRarity, 3f, 180f);
			DistributeEntities((GameObject)Resources.Load("containercrate"), 0.05f * totalLootRarity, 0.07f * totalLootRarity, 3f, 180f);
			DistributeEntities((GameObject)Resources.Load("foodbox"), 0.1f * totalLootRarity, 0.13f * totalLootRarity, 3f, 180f);
			DistributeEntities((GameObject)Resources.Load("spikestabber"), 0.4f * totalTrapRarity, 0.5f * totalTrapRarity, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("shadecrawler"), 0.4f * totalTrapRarity, 0.42f * totalTrapRarity, 2f, 180f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("corpse"), 1f * lootRarityMultiplier, 1.1f * lootRarityMultiplier, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int vector2Int) => GetBlock(vector2Int) > 0 && GetBlock(vector2Int + Vector2Int.right) > 0 && GetBlock(vector2Int - Vector2Int.right) > 0);
			DistributeEntities((GameObject)Resources.Load("animalcorpse"), 0.6f * lootRarityMultiplier, 0.7f * lootRarityMultiplier, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int vector2Int) => GetBlock(vector2Int) > 0 && GetBlock(vector2Int + Vector2Int.right) > 0 && GetBlock(vector2Int - Vector2Int.right) > 0);
			DistributeEntities((GameObject)Resources.Load("drillpod"), 0.09f, 0.1f, 0f, 0f, 0f, spawnInGround: true, randomFlip: true, null, isTrap: true);
			if (biomeDepth > 0)
			{
				DistributeEntities((GameObject)Resources.Load("barbedwirefence"), 0.6f * totalTrapRarity, 0.8f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("beartrap"), 0.2f * totalTrapRarity, 0.25f * totalTrapRarity, 1f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("CaveTicks"), 0.15f * totalTrapRarity, 0.2f * totalTrapRarity, 4f, 0f, 3f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("geyser"), 1.6f, 1.8f, 0.6f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3));
			}
			else
			{
				DistributeEntities((GameObject)Resources.Load("geyser"), 0.7f, 0.8f, 0.6f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3));
			}
			DistributeEntities((GameObject)Resources.Load("jumppad"), 0.6f * totalTrapRarity, 0.8f * totalTrapRarity, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.2f, 0.3f) * totalLootRarity;
			for (int num2 = 0; (float)num2 < num; num2++)
			{
				Vector2 vector = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth));
				if (!Physics2D.OverlapPoint(vector, LayerMask.GetMask("Ground")))
				{
					RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, CHUNKSIZE, LayerMask.GetMask("Ground"));
					if ((bool)raycastHit2D)
					{
						((GameObject)UnityEngine.Object.Instantiate(Resources.Load("bandage"), raycastHit2D.point + Vector2.up, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-0f, 360f)))).GetComponent<Item>().condition = (float)UnityEngine.Random.Range(1, 4) * 0.33f;
					}
				}
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.8f, 1f);
			for (int num3 = 0; (float)num3 < num; num3++)
			{
				Vector2 vector2 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth));
				if (!Physics2D.OverlapPoint(vector2, LayerMask.GetMask("Ground")) && !Physics2D.Raycast(vector2, Vector2.down, 12f, LayerMask.GetMask("Ground")))
				{
					RaycastHit2D raycastHit2D2 = Physics2D.Linecast(vector2, vector2 + Vector2.down * 25f, LayerMask.GetMask("Ground"));
					Vector2 vector3 = (raycastHit2D2 ? raycastHit2D2.point : (vector2 + Vector2.down * 25f));
					GameObject obj = Utils.Create("climbingropeextended", vector2, 0f);
					Climbable component = obj.GetComponent<Climbable>();
					component.points.Add(vector3);
					component.points.Add(vector2);
					obj.transform.GetChild(0).localScale = new Vector2(1f, Vector2.Distance(vector2, vector3) / 25f);
				}
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.3f, 0.5f);
			for (int num4 = 0; (float)num4 < num; num4++)
			{
				Vector2 vector4 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth));
				if (!Physics2D.OverlapPoint(vector4, LayerMask.GetMask("Ground")))
				{
					RaycastHit2D raycastHit2D3 = Physics2D.Raycast(vector4, Vector2.down, CHUNKSIZE, LayerMask.GetMask("Ground"));
					if ((bool)raycastHit2D3)
					{
						_ = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("droppings"), raycastHit2D3.point + Vector2.up, Quaternion.identity);
					}
				}
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.015f, 0.02f) * totalLootRarity;
			if (biomeDepth == 0)
			{
				for (int num5 = 0; (float)num5 < num; num5++)
				{
					Vector2 vector5 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth));
					if (!Physics2D.OverlapPoint(vector5, LayerMask.GetMask("Ground")))
					{
						RaycastHit2D raycastHit2D4 = Physics2D.Raycast(vector5, Vector2.down, CHUNKSIZE, LayerMask.GetMask("Ground"));
						if ((bool)raycastHit2D4)
						{
							_ = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("fleshchunk"), raycastHit2D4.point + Vector2.up, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-0f, 360f)));
						}
					}
				}
			}
			DistributeEntities((GameObject)Resources.Load("geotree"), 2.7f, 3f, 3f, 6f, 0.15f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("hydreed"), 1.4f, 1.6f, 2.6f, 6f, 0.4f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("leadbush"), 2f, 2.2f, 0.6f, 6f, 0.1f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
		}
		else if (biomeDepth == 2 || biomeDepth == 3)
		{
			PlaceCrystals();
			DistributeEntities((GameObject)Resources.Load("glowplant"), 2.4f * ((biomeDepth == 2) ? 1f : 2f), 2.5f * ((biomeDepth == 2) ? 1f : 2f), 1.25f, 10f, 0.25f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 12 || GetBlock(pos3) == 13 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("stoneplant"), 0.4f * ((biomeDepth == 2) ? 1f : 3f), 0.5f * ((biomeDepth == 2) ? 1f : 3f), 1.9f, 10f, 0.1f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 12 || GetBlock(pos3) == 13 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("cactus"), 1.4f, 1.6f, 2.1f, 10f, 0.3f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 12 || GetBlock(pos3) == 13 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("sandrose"), 1.3f, 1.4f, 1.5f, 10f, 0f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 12 || GetBlock(pos3) == 13 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("drybush"), 6f, 7f, 2f, 20f, 0f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 12 || GetBlock(pos3) == 13 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("brownshroom"), 4f, 5f, 0.9f, 10f, 0f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 12 || GetBlock(pos3) == 13 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("stalagmite"), 10f, 15f, 2.8f, 0f, 0.15f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 18 || GetBlock(pos3) == 17 || GetBlock(pos3) == 19);
			DistributeEntities((GameObject)Resources.Load("jumppad"), 0.25f * totalTrapRarity, 0.35f * totalTrapRarity, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("landmine"), 0.13f * totalTrapRarity, 0.16f * totalTrapRarity, 0.4f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("ceilingrye"), 0.08f, 0.1f, 1f, 10f, 0.5f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3), isTrap: false, Vector2.up);
			if (biomeDepth == 3)
			{
				DistributeEntities((GameObject)Resources.Load("spentfuel"), 0.3f * totalTrapRarity, 0.35f * totalTrapRarity, 1.875f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("soundcannon"), 0.4f * totalTrapRarity, 0.45f * totalTrapRarity, 1f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("foodbox"), 0.1f * totalLootRarity, 0.13f * totalLootRarity, 3f, 180f);
				DistributeEntities((GameObject)Resources.Load("pop"), 3f * totalLootRarity, 4f * totalLootRarity, 2f, 20f, 0.2f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) == 12 || GetBlock(pos3) == 13 || isSoil(pos3));
				DistributeEntities((GameObject)Resources.Load("coil"), 0.2f * totalTrapRarity, 0.3f * totalTrapRarity, 2f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			}
			else
			{
				DistributeEntities((GameObject)Resources.Load("wallbiter"), 0.12f * totalTrapRarity, 0.13f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("shadecrawler"), 0.2f * totalTrapRarity, 0.2f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("droppings"), 0.75f, 0.82f);
				DistributeEntities((GameObject)Resources.Load("beartrap"), 0.1f * totalTrapRarity, 0.2f * totalTrapRarity, 1f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
				DistributeEntities((GameObject)Resources.Load("barbedwirefence"), 0.7f * totalTrapRarity, 0.8f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			}
			float num6 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.3f, 0.4f);
			for (int num7 = 0; (float)num7 < num6; num7++)
			{
				UnityEngine.Object.Instantiate(position: new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight)), original: Resources.Load("oilpipe"), rotation: Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 360f));
			}
			num6 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.12f, 0.15f) * totalTrapRarity * ((biomeDepth == 2) ? 1f : 0.66f);
			for (int num8 = 0; (float)num8 < num6; num8++)
			{
				Vector2 vector6 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, PlayerCamera.main.body.transform.position.y - 5f));
				if (!Physics2D.OverlapPoint(vector6, LayerMask.GetMask("Ground")))
				{
					float num9 = ((UnityEngine.Random.value > 0.5f) ? 1f : (-1f));
					RaycastHit2D raycastHit2D5 = Physics2D.Raycast(vector6, Vector2.right * num9, CHUNKSIZE, LayerMask.GetMask("Ground"));
					if ((bool)raycastHit2D5)
					{
						((GameObject)UnityEngine.Object.Instantiate(Resources.Load("turret"), raycastHit2D5.point + Vector2.left * num9, Quaternion.identity)).transform.localScale = new Vector2(0f - num9, 1f);
					}
				}
			}
			num6 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(1.5f, 2f) * totalTrapRarity;
			for (int num10 = 0; (float)num10 < num6; num10++)
			{
				Vector2 vector7 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, PlayerCamera.main.body.transform.position.y - 5f));
				if (!Physics2D.OverlapPoint(vector7, LayerMask.GetMask("Ground")))
				{
					RaycastHit2D raycastHit2D6 = Physics2D.Raycast(vector7, Vector2.up, CHUNKSIZE * 2, LayerMask.GetMask("Ground"));
					if ((bool)raycastHit2D6 && raycastHit2D6.point.y < (float)halfHeight - 5f)
					{
						GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("stalactite"), raycastHit2D6.point + Vector2.down * 1.5f, Quaternion.identity);
						gameObject.GetComponent<BuildingEntity>().blockPlacedOn = WorldToBlockPos(raycastHit2D6.point + Vector2.up * 0.5f);
						ChunkUpdated[WorldToBlockPos(raycastHit2D6.point + Vector2.up * 0.5f).x / CHUNKSIZE, WorldToBlockPos(raycastHit2D6.point + Vector2.up * 0.5f).y / CHUNKSIZE].AddListener(gameObject.GetComponent<StalactiteDropper>().CheckSeating);
						gameObject.transform.localScale = new Vector3((UnityEngine.Random.Range(0f, 1f) > 0.5f) ? (-1f) : 1f, 1f, 1f);
					}
				}
			}
			num6 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(6f, 7f) * ((biomeDepth == 2) ? 1f : 0.1f);
			for (int num11 = 0; (float)num11 < num6; num11++)
			{
				Vector2 vector8 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight));
				if (!Physics2D.OverlapPoint(vector8, LayerMask.GetMask("Ground")))
				{
					RaycastHit2D raycastHit2D7 = Physics2D.Raycast(vector8, Vector2.up, CHUNKSIZE * 4, LayerMask.GetMask("Ground"));
					RaycastHit2D raycastHit2D8 = Physics2D.Raycast(vector8, Vector2.down, CHUNKSIZE * 4, LayerMask.GetMask("Ground"));
					Vector2Int pos = WorldToBlockPos(raycastHit2D7.point - Vector2.up * 0.5f);
					if ((bool)raycastHit2D7 && (bool)raycastHit2D8 && raycastHit2D8.point.y < (float)halfHeight - 5f)
					{
						Color color = Color.Lerp(Color.gray, Color.white, UnityEngine.Random.value);
						GameObject gameObject2 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Special/sandvinehook"), BlockToWorldPos(pos), Quaternion.identity);
						GameObject obj2 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Special/sandvinerope"), (BlockToWorldPos(pos) + raycastHit2D8.point) * 0.5f, Quaternion.identity);
						obj2.GetComponent<SpriteRenderer>().size = new Vector2(2.5f, Mathf.Abs(BlockToWorldPos(pos).y - raycastHit2D8.point.y));
						obj2.GetComponent<SpriteRenderer>().color = color;
						gameObject2.GetComponent<SpriteRenderer>().color = color;
						obj2.GetComponent<SpriteRenderer>().flipX = UnityEngine.Random.value > 0.5f;
						gameObject2.GetComponent<SpriteRenderer>().flipX = UnityEngine.Random.value > 0.5f;
						float num12 = UnityEngine.Random.Range(0.15f, 1f);
						gameObject2.transform.localScale = new Vector3(num12, 1f);
						obj2.transform.localScale = new Vector3(num12, 1f);
						float downwardsVelocity = (1f - num12) * 16f;
						Climbable component2 = obj2.GetComponent<Climbable>();
						component2.points.Add(raycastHit2D8.point);
						component2.points.Add(raycastHit2D7.point);
						component2.downwardsVelocity = downwardsVelocity;
					}
				}
			}
			DistributeEntities((GameObject)Resources.Load("rag"), 0.12f * lootRarityMultiplier * ((biomeDepth == 2) ? 1f : 2.5f), 0.2f * lootRarityMultiplier * ((biomeDepth == 2) ? 1f : 2.5f), 1f);
			DistributeEntities((GameObject)Resources.Load("corpse"), 0.75f * lootRarityMultiplier * ((biomeDepth == 2) ? 1f : 2f), 0.82f * lootRarityMultiplier * ((biomeDepth == 2) ? 1f : 2f), 0f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int vector2Int) => GetBlock(vector2Int) > 0 && GetBlock(vector2Int + Vector2Int.right) > 0 && GetBlock(vector2Int - Vector2Int.right) > 0);
		}
		else if (biomeDepth == 4)
		{
			float num13 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(4f, 5f);
			for (int num14 = 0; (float)num14 < num13; num14++)
			{
				Vector2 vector9 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight));
				if (!Physics2D.OverlapPoint(vector9, LayerMask.GetMask("Ground")))
				{
					RaycastHit2D raycastHit2D9 = Physics2D.Raycast(vector9, Vector2.up, CHUNKSIZE * 4, LayerMask.GetMask("Ground"));
					RaycastHit2D raycastHit2D10 = Physics2D.Raycast(vector9, Vector2.down, CHUNKSIZE * 4, LayerMask.GetMask("Ground"));
					Vector2Int pos2 = WorldToBlockPos(raycastHit2D9.point - Vector2.up * 0.5f);
					if ((bool)raycastHit2D9 && (bool)raycastHit2D10 && raycastHit2D10.point.y < (float)halfHeight - 5f)
					{
						Color color2 = Color.Lerp(Color.gray, Color.white, UnityEngine.Random.value);
						GameObject gameObject3 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Special/sandvinehook"), BlockToWorldPos(pos2), Quaternion.identity);
						GameObject obj3 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Special/sandvinerope"), (BlockToWorldPos(pos2) + raycastHit2D10.point) * 0.5f, Quaternion.identity);
						obj3.GetComponent<SpriteRenderer>().size = new Vector2(2.5f, Mathf.Abs(BlockToWorldPos(pos2).y - raycastHit2D10.point.y));
						obj3.GetComponent<SpriteRenderer>().color = color2;
						gameObject3.GetComponent<SpriteRenderer>().color = color2;
						obj3.GetComponent<SpriteRenderer>().flipX = UnityEngine.Random.value > 0.5f;
						gameObject3.GetComponent<SpriteRenderer>().flipX = UnityEngine.Random.value > 0.5f;
						float num15 = UnityEngine.Random.Range(0.15f, 1f);
						gameObject3.transform.localScale = new Vector3(num15, 1f);
						obj3.transform.localScale = new Vector3(num15, 1f);
						float downwardsVelocity2 = (1f - num15) * 16f;
						Climbable component3 = obj3.GetComponent<Climbable>();
						component3.points.Add(raycastHit2D10.point);
						component3.points.Add(raycastHit2D9.point);
						component3.downwardsVelocity = downwardsVelocity2;
					}
				}
			}
			DistributeEntities((GameObject)Resources.Load("glowplant"), 0.2f, 0.3f, 1.25f, 10f, 0.25f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("shadecrawler"), 0.45f * totalTrapRarity, 0.5f * totalTrapRarity, 2f, 180f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("wallbiter"), 0.1f * totalTrapRarity, 0.11f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("thornbackyoung"), 0.24f * totalTrapRarity, 0.26f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("overgrowntick"), 0.1f * totalTrapRarity, 0.12f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("caveticks"), 0.15f * totalTrapRarity, 0.16f * totalTrapRarity, 4.8f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			for (int num16 = 0; num16 < 3; num16++)
			{
				UnityEngine.Object.Instantiate(Resources.Load("thornbackelder"), new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, body.transform.position.y - 20f)), Quaternion.identity);
			}
			DistributeEntities((GameObject)Resources.Load("stoneplant"), 0.4f, 0.5f, 1.9f, 10f, 0.1f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("ceilingrye"), 0.65f, 0.8f, 1f, 10f, 0.5f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3), isTrap: false, Vector2.up);
			DistributeEntities((GameObject)Resources.Load("medcrate"), 0.18f * totalLootRarity, 0.2f * totalLootRarity, 3f, 180f);
			DistributeEntities((GameObject)Resources.Load("containercrate"), 0.05f * totalLootRarity, 0.07f * totalLootRarity, 3f, 180f);
			DistributeEntities((GameObject)Resources.Load("foodbox"), 0.1f * totalLootRarity, 0.13f * totalLootRarity, 3f, 180f);
			DistributeEntities((GameObject)Resources.Load("corpse"), 1.1f * lootRarityMultiplier, 1.2f * lootRarityMultiplier, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int vector2Int) => GetBlock(vector2Int) > 0 && GetBlock(vector2Int + Vector2Int.right) > 0 && GetBlock(vector2Int - Vector2Int.right) > 0);
			DistributeEntities((GameObject)Resources.Load("animalcorpse"), 0.9f * lootRarityMultiplier, 0.95f * lootRarityMultiplier, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int vector2Int) => GetBlock(vector2Int) > 0 && GetBlock(vector2Int + Vector2Int.right) > 0 && GetBlock(vector2Int - Vector2Int.right) > 0);
			DistributeEntities((GameObject)Resources.Load("geotree"), 0.4f, 0.5f, 3f, 6f, 0.15f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("browncap"), 0.4f, 0.5f, 3f, 6f, 0.15f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("hydreed"), 0.6f, 0.7f, 2.6f, 6f, 0.4f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("leadbush"), 1.1f, 1.2f, 0.6f, 6f, 0.1f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("droppings"), 3.7f, 4f);
			DistributeEntities((GameObject)Resources.Load("pop"), 1f * totalLootRarity, 1.1f * totalLootRarity, 2f, 20f, 0.2f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("bananaplant"), 1.9f * totalTrapRarity, 2f * totalTrapRarity, 0.4f, 15f, 0.1f, spawnInGround: false, randomFlip: true, (Vector2Int pos3) => isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("coil"), 0.2f * totalTrapRarity, 0.3f * totalTrapRarity, 2f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("beartrap"), 0.1f * totalTrapRarity, 0.2f * totalTrapRarity, 1f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("jumppad"), 0.25f * totalTrapRarity, 0.35f * totalTrapRarity, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("spikestabber"), 0.4f * totalTrapRarity, 0.5f * totalTrapRarity, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("grabberplant"), 0.4f * totalTrapRarity, 0.5f * totalTrapRarity, 0f, 0f, 0f, spawnInGround: false, randomFlip: false, null, isTrap: true);
			DistributeEntities((GameObject)Resources.Load("geyser"), 0.7f, 0.8f, 0.6f, 0f, 0f, spawnInGround: false, randomFlip: false, (Vector2Int pos3) => GetBlock(pos3) < 3 || isSoil(pos3));
			DistributeEntities((GameObject)Resources.Load("skullcrusher"), 1.1f, 1.2f, 1f, 10f, 0f, spawnInGround: false, randomFlip: true, null, isTrap: false, Vector2.up);
			num13 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(6f, 7f);
			for (int num17 = 0; (float)num17 < num13; num17++)
			{
				UnityEngine.Object.Instantiate(position: new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight)), original: Resources.Load("wallflower"), rotation: Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 360f));
			}
			PlaceCrystals();
		}
		if (biomeOverride == OverrideSceneType.Debug)
		{
			Utils.Create("LifePodLight", body.transform.position + Vector3.up * 10f, 180f);
		}
	}

	private IEnumerator WorldGenerateWorldBorders()
	{
		SetLoadingText("gengeneratingborders");
		yield return null;
		float num = 0.125f;
		for (int i = 0; i < 8; i++)
		{
			for (int j = 0; j < world.height; j++)
			{
				if (UnityEngine.Random.Range(0f, 1f) > (float)i * num)
				{
					worldBlocks[i, j] = 14;
				}
			}
		}
		for (int k = 0; k < 8; k++)
		{
			for (int l = 0; l < world.height; l++)
			{
				if (UnityEngine.Random.Range(0f, 1f) > (float)k * num)
				{
					worldBlocks[(int)checked((nint)unchecked(width - 1 - k)), l] = 14;
				}
			}
		}
	}

	private IEnumerator WorldPlacePlayer()
	{
		SetLoadingText("genplacingplayer");
		yield return null;
		PlayerCamera.main.body.PlaceBody();
		if (totalTraveled <= 0 && biomeOverride == OverrideSceneType.None && debugStartDepth == 0)
		{
			GenerateBlockCircle(PlayerCamera.main.body.transform.position, 30, 3, 0.8f, 0f, autoUpdateChunk: true);
			GenerateBlockCircle(PlayerCamera.main.body.transform.position, 36, 4, 0.3f, 0f, autoUpdateChunk: true);
			GenerateBlockCircle(PlayerCamera.main.body.transform.position, 30, 0, 0.15f, 0f, autoUpdateChunk: true);
			GenerateObjectAtPos(WorldToBlockPos(PlayerCamera.main.body.transform.position + Vector3.up * 4f), Resources.Load<GameObject>("LifepodStart").transform.GetChild(0).GetComponent<Tilemap>(), 0.97f);
			GenerateEntityAtPos(BlockToWorldPos(WorldToBlockPos(PlayerCamera.main.body.transform.position + Vector3.up * 4f)), Resources.Load<GameObject>("Lifepod"));
			UnityEngine.Object.Instantiate(((GameObject)Resources.Load("LifepodStart")).transform.GetChild(0).GetChild(0).gameObject, PlayerCamera.main.body.transform.position + Vector3.up * 4f, Quaternion.identity);
			switch (GetRunSettingInt("startingsupplies"))
			{
			case 1:
				body.PickUpItem(Utils.Create("emergencylight", body.transform.position, 0f).GetComponent<Item>(), 3, force: true);
				break;
			case 2:
				body.PickUpItem(Utils.Create("lantern", body.transform.position, 0f).GetComponent<Item>(), 3, force: true);
				body.PickUpItem(Utils.Create("dogfood", body.transform.position, 0f).GetComponent<Item>(), 4, force: true);
				body.PickUpItem(Utils.Create("waterbottle", body.transform.position, 0f).GetComponent<Item>(), 5, force: true);
				body.PickUpItem(Utils.Create("trashbag", body.transform.position, 0f).GetComponent<Item>(), 1, force: true);
				break;
			}
			if (DateTime.Now.Month == 12)
			{
				Utils.Create("present", (Vector2)body.transform.position + Vector2.right * UnityEngine.Random.Range(-2f, 2f), 0f);
				Utils.Create("Special/holidaytree", body.transform.position, 0f);
			}
		}
	}

	private IEnumerator WorldGenerateStructures()
	{
		SetLoadingText("gencreatingstructures");
		yield return null;
		if (biomeDepth <= 1 && biomeOverride == OverrideSceneType.None)
		{
			GenerateDropCapsules(UnityEngine.Random.Range(0.12f, 0.13f));
			GenerateCollapsedPods(UnityEngine.Random.Range(0.055f, 0.066f));
			yield return null;
			float num;
			if (biomeDepth > 0)
			{
				num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.05f, 0.07f) * totalLootRarity;
				for (int i = 0; (float)i < num; i++)
				{
					Vector2 vector = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
					RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, 400f, LayerMask.GetMask("Ground"));
					if ((bool)raycastHit2D)
					{
						vector = raycastHit2D.point;
					}
					Vector2Int pos = WorldToBlockPos(vector);
					GenerateBlockCircle(vector, 16, 3, 0.8f, 0f, autoUpdateChunk: true);
					GenerateBlockCircle(vector, 20, 4, 0.3f, 0f, autoUpdateChunk: true);
					GenerateBlockCircle(vector, 16, 0, 0.15f, 0f, autoUpdateChunk: true);
					GenerateObjectAtPos(pos, Resources.Load<GameObject>("BioContainer").transform.GetChild(0).GetComponent<Tilemap>(), 1f, genMode: true);
					GenerateEntityAtPos(BlockToWorldPos(pos), Resources.Load<GameObject>("BioContainer"));
				}
				num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.09f, 0.12f) * totalLootRarity;
				for (int j = 0; (float)j < num; j++)
				{
					Vector2 pos2 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
					Vector2Int pos3 = WorldToBlockPos(pos2);
					GenerateObjectAtPos(pos3, Resources.Load<GameObject>("Structures/SteelBridge").GetComponent<Tilemap>(), 0.85f, genMode: true);
					GenerateEntityAtPos(BlockToWorldPos(pos3), Resources.Load<GameObject>("Structures/SteelBridge"));
				}
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.06f, 0.08f) * totalLootRarity;
			for (int k = 0; (float)k < num; k++)
			{
				Vector2 vector2 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D2 = Physics2D.Raycast(vector2, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D2)
				{
					vector2 = raycastHit2D2.point;
				}
				Vector2Int pos4 = WorldToBlockPos(vector2);
				GenerateBlockCircle(vector2, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector2, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos4, Resources.Load<GameObject>("Structures/CratePod").GetComponent<Tilemap>(), 0.82f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos4), Resources.Load<GameObject>("Structures/CratePod"));
			}
			for (int l = 0; (float)l < num; l++)
			{
				Vector2 vector3 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D3 = Physics2D.Raycast(vector3, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D3)
				{
					vector3 = raycastHit2D3.point;
				}
				Vector2Int pos5 = WorldToBlockPos(vector3);
				GenerateBlockCircle(vector3, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector3, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos5, Resources.Load<GameObject>("Structures/MiniPod").GetComponent<Tilemap>(), 0.88f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos5), Resources.Load<GameObject>("Structures/MiniPod"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.045f, 0.05f) * totalLootRarity;
			for (int m = 0; (float)m < num; m++)
			{
				Vector2 vector4 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D4 = Physics2D.Raycast(vector4, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D4)
				{
					vector4 = raycastHit2D4.point;
				}
				Vector2Int pos6 = WorldToBlockPos(vector4);
				GenerateBlockCircle(vector4, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector4, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos6, Resources.Load<GameObject>("Structures/SteelThing").GetComponent<Tilemap>(), 0.9f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int n = 0; (float)n < num; n++)
			{
				Vector2 vector5 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D5 = Physics2D.Raycast(vector5, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D5)
				{
					vector5 = raycastHit2D5.point;
				}
				Vector2Int pos7 = WorldToBlockPos(vector5);
				GenerateObjectAtPos(pos7, Resources.Load<GameObject>("Structures/WoodCross").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num2 = 0; (float)num2 < num; num2++)
			{
				Vector2 vector6 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D6 = Physics2D.Raycast(vector6, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D6)
				{
					vector6 = raycastHit2D6.point;
				}
				Vector2Int pos8 = WorldToBlockPos(vector6);
				GenerateObjectAtPos(pos8, Resources.Load<GameObject>("Structures/WoodHorizontal").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			GenerateLifePods(UnityEngine.Random.Range(0.088f, 0.1f));
		}
		else if (biomeDepth == 2 || biomeDepth == 3)
		{
			GenerateDropCapsules(UnityEngine.Random.Range(0.12f, 0.13f));
			GenerateCollapsedPods(UnityEngine.Random.Range(0.066f, 0.077f) * ((biomeDepth == 2) ? 1f : 2.5f));
			yield return null;
			float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.05f, 0.07f) * totalLootRarity;
			for (int num3 = 0; (float)num3 < num; num3++)
			{
				Vector2 vector7 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D7 = Physics2D.Raycast(vector7, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D7)
				{
					vector7 = raycastHit2D7.point;
				}
				Vector2Int pos9 = WorldToBlockPos(vector7);
				GenerateBlockCircle(vector7, 16, 3, 0.8f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector7, 20, 4, 0.3f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector7, 16, 0, 0.15f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos9, Resources.Load<GameObject>("BioContainer").transform.GetChild(0).GetComponent<Tilemap>(), 1f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos9), Resources.Load<GameObject>("BioContainer"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.04f, 0.05f) * totalLootRarity;
			for (int num4 = 0; (float)num4 < num; num4++)
			{
				Vector2 vector8 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D8 = Physics2D.Raycast(vector8, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D8)
				{
					vector8 = raycastHit2D8.point;
				}
				Vector2Int pos10 = WorldToBlockPos(vector8);
				GenerateObjectAtPos(pos10, Resources.Load<GameObject>("Structures/MedicalBuilding").GetComponent<Tilemap>(), 0.98f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos10), Resources.Load<GameObject>("Structures/MedicalBuilding"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.09f, 0.12f) * totalLootRarity;
			for (int num5 = 0; (float)num5 < num; num5++)
			{
				Vector2 pos11 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				Vector2Int pos12 = WorldToBlockPos(pos11);
				GenerateObjectAtPos(pos12, Resources.Load<GameObject>("Structures/SteelBridge").GetComponent<Tilemap>(), 0.95f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos12), Resources.Load<GameObject>("Structures/SteelBridge"));
			}
			for (int num6 = 0; (float)num6 < num; num6++)
			{
				Vector2 vector9 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D9 = Physics2D.Raycast(vector9, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D9)
				{
					vector9 = raycastHit2D9.point;
				}
				Vector2Int pos13 = WorldToBlockPos(vector9);
				GenerateBlockCircle(vector9, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector9, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos13, Resources.Load<GameObject>("Structures/MiniPod").GetComponent<Tilemap>(), 0.88f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos13), Resources.Load<GameObject>("Structures/MiniPod"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num7 = 0; (float)num7 < num; num7++)
			{
				Vector2 vector10 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D10 = Physics2D.Raycast(vector10, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D10)
				{
					vector10 = raycastHit2D10.point;
				}
				Vector2Int pos14 = WorldToBlockPos(vector10);
				GenerateObjectAtPos(pos14, Resources.Load<GameObject>("Structures/WoodCross").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num8 = 0; (float)num8 < num; num8++)
			{
				Vector2 vector11 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D11 = Physics2D.Raycast(vector11, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D11)
				{
					vector11 = raycastHit2D11.point;
				}
				Vector2Int pos15 = WorldToBlockPos(vector11);
				GenerateObjectAtPos(pos15, Resources.Load<GameObject>("Structures/WoodHorizontal").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			GenerateLifePods(UnityEngine.Random.Range(0.088f, 0.1f));
		}
		else if (biomeDepth == 4)
		{
			GenerateDropCapsules(UnityEngine.Random.Range(0.12f, 0.13f));
			GenerateCollapsedPods(UnityEngine.Random.Range(0.066f, 0.077f));
			yield return null;
			float num9 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.9f, 1.1f);
			for (int num10 = 0; (float)num10 < num9; num10++)
			{
				RaycastHit2D raycastHit2D12 = Physics2D.Raycast(new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight)), Vector2.down, CHUNKSIZE, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D12)
				{
					GenerateTree(WorldToBlockPos(raycastHit2D12.point));
				}
			}
			float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.06f, 0.08f) * totalLootRarity;
			for (int num11 = 0; (float)num11 < num; num11++)
			{
				Vector2 vector12 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D13 = Physics2D.Raycast(vector12, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D13)
				{
					vector12 = raycastHit2D13.point;
				}
				Vector2Int pos16 = WorldToBlockPos(vector12);
				GenerateBlockCircle(vector12, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector12, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos16, Resources.Load<GameObject>("Structures/CratePod").GetComponent<Tilemap>(), 0.82f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos16), Resources.Load<GameObject>("Structures/CratePod"));
			}
			for (int num12 = 0; (float)num12 < num; num12++)
			{
				Vector2 vector13 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D14 = Physics2D.Raycast(vector13, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D14)
				{
					vector13 = raycastHit2D14.point;
				}
				Vector2Int pos17 = WorldToBlockPos(vector13);
				GenerateBlockCircle(vector13, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector13, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos17, Resources.Load<GameObject>("Structures/MiniPod").GetComponent<Tilemap>(), 0.88f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos17), Resources.Load<GameObject>("Structures/MiniPod"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num13 = 0; (float)num13 < num; num13++)
			{
				Vector2 vector14 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D15 = Physics2D.Raycast(vector14, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D15)
				{
					vector14 = raycastHit2D15.point;
				}
				Vector2Int pos18 = WorldToBlockPos(vector14);
				GenerateObjectAtPos(pos18, Resources.Load<GameObject>("Structures/WoodCross").GetComponent<Tilemap>(), 0.95f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num14 = 0; (float)num14 < num; num14++)
			{
				Vector2 vector15 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D16 = Physics2D.Raycast(vector15, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D16)
				{
					vector15 = raycastHit2D16.point;
				}
				Vector2Int pos19 = WorldToBlockPos(vector15);
				GenerateObjectAtPos(pos19, Resources.Load<GameObject>("Structures/WoodHorizontal").GetComponent<Tilemap>(), 0.95f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.04f, 0.05f) * totalLootRarity;
			for (int num15 = 0; (float)num15 < num; num15++)
			{
				Vector2 vector16 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D17 = Physics2D.Raycast(vector16, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D17)
				{
					vector16 = raycastHit2D17.point;
				}
				Vector2Int pos20 = WorldToBlockPos(vector16);
				GenerateObjectAtPos(pos20, Resources.Load<GameObject>("Structures/BrickLoot").GetComponent<Tilemap>(), 0.925f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos20), Resources.Load<GameObject>("Structures/BrickLoot"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.04f) * totalLootRarity;
			for (int num16 = 0; (float)num16 < num; num16++)
			{
				Vector2 vector17 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D18 = Physics2D.Raycast(vector17, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D18)
				{
					vector17 = raycastHit2D18.point;
				}
				Vector2Int pos21 = WorldToBlockPos(vector17);
				GenerateBlockCircle(vector17, 16, 3, 0.8f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector17, 20, 4, 0.3f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector17, 16, 0, 0.15f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos21, Resources.Load<GameObject>("BioContainer").transform.GetChild(0).GetComponent<Tilemap>(), 0.975f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos21), Resources.Load<GameObject>("BioContainer"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.35f, 0.5f);
			for (int num17 = 0; (float)num17 < num; num17++)
			{
				Vector2Int vector2Int = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
				int num18 = UnityEngine.Random.Range(1, 5);
				for (int num19 = 0; num19 < num18; num19++)
				{
					for (int num20 = 0; num20 < 1; num20++)
					{
						if (vector2Int.x + num19 < width && vector2Int.y + num20 < height)
						{
							worldBlocks[num19 + vector2Int.x, num20 + vector2Int.y] = 5;
						}
					}
				}
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.35f, 0.5f);
			for (int num21 = 0; (float)num21 < num; num21++)
			{
				Vector2Int vector2Int2 = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
				int num22 = UnityEngine.Random.Range(1, 5);
				for (int num23 = 0; num23 < num22; num23++)
				{
					for (int num24 = 0; num24 < 2; num24++)
					{
						if (vector2Int2.x + num24 < width && vector2Int2.y + num23 < height)
						{
							worldBlocks[num24 + vector2Int2.x, num23 + vector2Int2.y] = 5;
						}
					}
				}
			}
			GenerateLifePods(UnityEngine.Random.Range(0.088f, 0.1f));
		}
		else if (biomeDepth == 5)
		{
			GenerateDropCapsules(UnityEngine.Random.Range(0.12f, 0.13f));
			GenerateCollapsedPods(UnityEngine.Random.Range(0.055f, 0.066f));
			yield return null;
			float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.06f, 0.08f) * totalLootRarity;
			for (int num25 = 0; (float)num25 < num; num25++)
			{
				Vector2 vector18 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D19 = Physics2D.Raycast(vector18, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D19)
				{
					vector18 = raycastHit2D19.point;
				}
				Vector2Int pos22 = WorldToBlockPos(vector18);
				GenerateBlockCircle(vector18, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector18, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos22, Resources.Load<GameObject>("Structures/CratePod").GetComponent<Tilemap>(), 0.82f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos22), Resources.Load<GameObject>("Structures/CratePod"));
			}
			for (int num26 = 0; (float)num26 < num; num26++)
			{
				Vector2 vector19 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D20 = Physics2D.Raycast(vector19, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D20)
				{
					vector19 = raycastHit2D20.point;
				}
				Vector2Int pos23 = WorldToBlockPos(vector19);
				GenerateBlockCircle(vector19, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector19, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos23, Resources.Load<GameObject>("Structures/MiniPod").GetComponent<Tilemap>(), 0.88f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos23), Resources.Load<GameObject>("Structures/MiniPod"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.045f, 0.05f) * totalLootRarity;
			for (int num27 = 0; (float)num27 < num; num27++)
			{
				Vector2 vector20 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D21 = Physics2D.Raycast(vector20, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D21)
				{
					vector20 = raycastHit2D21.point;
				}
				Vector2Int pos24 = WorldToBlockPos(vector20);
				GenerateBlockCircle(vector20, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector20, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos24, Resources.Load<GameObject>("Structures/SteelThing").GetComponent<Tilemap>(), 0.9f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num28 = 0; (float)num28 < num; num28++)
			{
				Vector2 vector21 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D22 = Physics2D.Raycast(vector21, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D22)
				{
					vector21 = raycastHit2D22.point;
				}
				Vector2Int pos25 = WorldToBlockPos(vector21);
				GenerateObjectAtPos(pos25, Resources.Load<GameObject>("Structures/WoodCross").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num29 = 0; (float)num29 < num; num29++)
			{
				Vector2 vector22 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D23 = Physics2D.Raycast(vector22, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D23)
				{
					vector22 = raycastHit2D23.point;
				}
				Vector2Int pos26 = WorldToBlockPos(vector22);
				GenerateObjectAtPos(pos26, Resources.Load<GameObject>("Structures/WoodHorizontal").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			GenerateLifePods(UnityEngine.Random.Range(0.088f, 0.1f));
		}
		else if (biomeDepth == 6)
		{
			float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.9f, 1.1f);
			for (int num30 = 0; (float)num30 < num; num30++)
			{
				RaycastHit2D raycastHit2D24 = Physics2D.Raycast(new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight)), Vector2.down, CHUNKSIZE, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D24)
				{
					GenerateBigMushroom(WorldToBlockPos(raycastHit2D24.point));
				}
			}
			GenerateDropCapsules(UnityEngine.Random.Range(0.12f, 0.13f));
			GenerateCollapsedPods(UnityEngine.Random.Range(0.055f, 0.066f));
			yield return null;
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.05f, 0.07f) * totalLootRarity;
			for (int num31 = 0; (float)num31 < num; num31++)
			{
				Vector2 vector23 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D25 = Physics2D.Raycast(vector23, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D25)
				{
					vector23 = raycastHit2D25.point;
				}
				Vector2Int pos27 = WorldToBlockPos(vector23);
				GenerateBlockCircle(vector23, 16, 3, 0.8f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector23, 20, 4, 0.3f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector23, 16, 0, 0.15f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos27, Resources.Load<GameObject>("BioContainer").transform.GetChild(0).GetComponent<Tilemap>(), 1f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos27), Resources.Load<GameObject>("BioContainer"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.09f, 0.12f) * totalLootRarity;
			for (int num32 = 0; (float)num32 < num; num32++)
			{
				Vector2 pos28 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				Vector2Int pos29 = WorldToBlockPos(pos28);
				GenerateObjectAtPos(pos29, Resources.Load<GameObject>("Structures/SteelBridge").GetComponent<Tilemap>(), 0.85f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos29), Resources.Load<GameObject>("Structures/SteelBridge"));
			}
			for (int num33 = 0; (float)num33 < num; num33++)
			{
				Vector2 vector24 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D26 = Physics2D.Raycast(vector24, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D26)
				{
					vector24 = raycastHit2D26.point;
				}
				Vector2Int pos30 = WorldToBlockPos(vector24);
				GenerateBlockCircle(vector24, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector24, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos30, Resources.Load<GameObject>("Structures/MiniPod").GetComponent<Tilemap>(), 0.88f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos30), Resources.Load<GameObject>("Structures/MiniPod"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num34 = 0; (float)num34 < num; num34++)
			{
				Vector2 vector25 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D27 = Physics2D.Raycast(vector25, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D27)
				{
					vector25 = raycastHit2D27.point;
				}
				Vector2Int pos31 = WorldToBlockPos(vector25);
				GenerateObjectAtPos(pos31, Resources.Load<GameObject>("Structures/WoodCross").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num35 = 0; (float)num35 < num; num35++)
			{
				Vector2 vector26 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D28 = Physics2D.Raycast(vector26, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D28)
				{
					vector26 = raycastHit2D28.point;
				}
				Vector2Int pos32 = WorldToBlockPos(vector26);
				GenerateObjectAtPos(pos32, Resources.Load<GameObject>("Structures/WoodHorizontal").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			GenerateLifePods(UnityEngine.Random.Range(0.088f, 0.1f));
		}
		else
		{
			if (biomeDepth != 7)
			{
				yield break;
			}
			GenerateDropCapsules(UnityEngine.Random.Range(0.12f, 0.13f));
			GenerateCollapsedPods(UnityEngine.Random.Range(0.15f, 0.2f));
			yield return null;
			float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.06f, 0.08f) * totalLootRarity;
			for (int num36 = 0; (float)num36 < num; num36++)
			{
				Vector2 vector27 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D29 = Physics2D.Raycast(vector27, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D29)
				{
					vector27 = raycastHit2D29.point;
				}
				Vector2Int pos33 = WorldToBlockPos(vector27);
				GenerateBlockCircle(vector27, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector27, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos33, Resources.Load<GameObject>("Structures/CratePod").GetComponent<Tilemap>(), 0.82f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos33), Resources.Load<GameObject>("Structures/CratePod"));
			}
			for (int num37 = 0; (float)num37 < num; num37++)
			{
				Vector2 vector28 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D30 = Physics2D.Raycast(vector28, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D30)
				{
					vector28 = raycastHit2D30.point;
				}
				Vector2Int pos34 = WorldToBlockPos(vector28);
				GenerateBlockCircle(vector28, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector28, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos34, Resources.Load<GameObject>("Structures/MiniPod").GetComponent<Tilemap>(), 0.88f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos34), Resources.Load<GameObject>("Structures/MiniPod"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.045f, 0.05f) * totalLootRarity;
			for (int num38 = 0; (float)num38 < num; num38++)
			{
				Vector2 vector29 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D31 = Physics2D.Raycast(vector29, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D31)
				{
					vector29 = raycastHit2D31.point;
				}
				Vector2Int pos35 = WorldToBlockPos(vector29);
				GenerateBlockCircle(vector29, 16, 3, 0.5f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector29, 20, 4, 0.2f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos35, Resources.Load<GameObject>("Structures/SteelThing").GetComponent<Tilemap>(), 0.9f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num39 = 0; (float)num39 < num; num39++)
			{
				Vector2 vector30 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D32 = Physics2D.Raycast(vector30, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D32)
				{
					vector30 = raycastHit2D32.point;
				}
				Vector2Int pos36 = WorldToBlockPos(vector30);
				GenerateObjectAtPos(pos36, Resources.Load<GameObject>("Structures/WoodCross").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.03f, 0.05f) * totalLootRarity;
			for (int num40 = 0; (float)num40 < num; num40++)
			{
				Vector2 vector31 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D33 = Physics2D.Raycast(vector31, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D33)
				{
					vector31 = raycastHit2D33.point;
				}
				Vector2Int pos37 = WorldToBlockPos(vector31);
				GenerateObjectAtPos(pos37, Resources.Load<GameObject>("Structures/WoodHorizontal").GetComponent<Tilemap>(), 0.94f, genMode: true);
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.05f, 0.07f) * totalLootRarity;
			for (int num41 = 0; (float)num41 < num; num41++)
			{
				Vector2 vector32 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				RaycastHit2D raycastHit2D34 = Physics2D.Raycast(vector32, Vector2.down, 400f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D34)
				{
					vector32 = raycastHit2D34.point;
				}
				Vector2Int pos38 = WorldToBlockPos(vector32);
				GenerateBlockCircle(vector32, 16, 3, 0.8f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector32, 20, 4, 0.3f, 0f, autoUpdateChunk: true);
				GenerateBlockCircle(vector32, 16, 0, 0.15f, 0f, autoUpdateChunk: true);
				GenerateObjectAtPos(pos38, Resources.Load<GameObject>("BioContainer").transform.GetChild(0).GetComponent<Tilemap>(), 1f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos38), Resources.Load<GameObject>("BioContainer"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.09f, 0.12f) * totalLootRarity;
			for (int num42 = 0; (float)num42 < num; num42++)
			{
				Vector2 pos39 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				Vector2Int pos40 = WorldToBlockPos(pos39);
				GenerateObjectAtPos(pos40, Resources.Load<GameObject>("Structures/SteelBridge").GetComponent<Tilemap>(), 0.85f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos40), Resources.Load<GameObject>("Structures/SteelBridge"));
			}
			num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.07f, 0.1f);
			for (int num43 = 0; (float)num43 < num; num43++)
			{
				Vector2 pos41 = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
				Vector2Int pos42 = WorldToBlockPos(pos41);
				GenerateObjectAtPos(pos42, Resources.Load<GameObject>("Structures/LongCorridor").GetComponent<Tilemap>(), 0.95f, genMode: true);
				GenerateEntityAtPos(BlockToWorldPos(pos42), Resources.Load<GameObject>("Structures/LongCorridor"));
			}
			GenerateObjectAtPos(new Vector2Int((int)halfWidth, (int)(height - 30)), Resources.Load<GameObject>("Structures/CrystalSpawnPlatform").GetComponent<Tilemap>(), 1f, genMode: true);
			GenerateEntityAtPos(BlockToWorldPos(new Vector2Int((int)halfWidth, (int)(height - 30))), Resources.Load<GameObject>("Structures/CrystalSpawnPlatform"));
		}
	}

	private IEnumerator WorldGenerateTerrain()
	{
		SetLoadingText("gencreatingterrain");
		yield return null;
		int tileCounter = 0;
		if (biomeOverride == OverrideSceneType.Tutorial)
		{
			GenerateObjectAtPosFast(Vector2Int.one * (int)halfWidth, Resources.Load<GameObject>("Special/TutorialStructure").transform.GetChild(0).GetComponent<Tilemap>());
			GenerateEntityAtPos(Vector2.one * 0.5f, Resources.Load<GameObject>("Special/TutorialStructure"));
		}
		else if (biomeOverride == OverrideSceneType.Debug)
		{
			for (int i = 0; i < width; i++)
			{
				for (int j = 0; j < height; j++)
				{
					if (j < halfHeight)
					{
						worldBlocks[i, j] = 1;
					}
				}
				tileCounter++;
				if (tileCounter > 100)
				{
					tileCounter = 0;
					yield return null;
				}
			}
		}
		else if (biomeDepth <= 1)
		{
			FastNoiseLite caveNoise = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			caveNoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
			caveNoise.SetFrequency(0.06f);
			caveNoise.SetFractalOctaves(3);
			caveNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
			caveNoise.SetFractalLacunarity(1.5f);
			FastNoiseLite dirtPerlinNoise = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			dirtPerlinNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
			dirtPerlinNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
			dirtPerlinNoise.SetFractalOctaves(7);
			dirtPerlinNoise.SetFrequency(0.035f);
			FastNoiseLite frequencyMap = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			frequencyMap.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
			frequencyMap.SetFrequency(0.00037f);
			FastNoiseLite biomeMap = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			biomeMap.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
			biomeMap.SetFrequency(0.04f);
			biomeMap.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.EuclideanSq);
			biomeMap.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
			biomeMap.SetCellularJitter(1f);
			biomeMap.SetFractalType(FastNoiseLite.FractalType.Ridged);
			biomeMap.SetFractalLacunarity(1.5f);
			for (int i = 0; i < width; i++)
			{
				for (int k = 0; k < height; k++)
				{
					caveNoise.SetFrequency(0.06f + frequencyMap.GetNoise(i, k) * 0.01f);
					worldBlocks[i, k] = ((caveNoise.GetNoise(i, k) > -0.715f) ? ((ushort)1) : ((ushort)0));
					float noise = dirtPerlinNoise.GetNoise(i, k);
					if (worldBlocks[i, k] > 0 && noise < -0.1f)
					{
						worldBlocks[i, k] = (ushort)(((double)noise < -0.33) ? 16 : 2);
					}
					if (worldBlocks[i, k] > 0 && UnityEngine.Random.Range(0f, 1f) > 0.99f)
					{
						worldBlocks[i, k] = (ushort)UnityEngine.Random.Range(1, 5);
					}
					if (biomeMap.GetNoise(i, k) > 0.1f)
					{
						worldBlocks[i, k] = (ushort)UnityEngine.Random.Range(3, 5);
					}
					if (worldBlocks[i, k] > 0 && biomeMap.GetNoise(i, k) < -0.8f)
					{
						worldBlocks[i, k] = 15;
					}
					if (biomeDepth == 1 && (float)k < (float)height * 0.5f)
					{
						float num = (float)k / (float)height * 2f;
						if (UnityEngine.Random.Range(0f, 1f) > num && worldBlocks[i, k] == 2)
						{
							worldBlocks[i, k] = 12;
						}
						if ((float)k < (float)height * 0.33f && UnityEngine.Random.Range(0f, 1f) > num * 3f && worldBlocks[i, k] == 1)
						{
							worldBlocks[i, k] = 13;
						}
					}
				}
				tileCounter++;
				if (tileCounter > 100)
				{
					tileCounter = 0;
					yield return null;
				}
			}
			GenerateOres();
			_ = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.35f, 0.5f);
			if (biomeDepth > 0)
			{
				float num2 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.35f, 0.5f);
				for (int l = 0; (float)l < num2; l++)
				{
					Vector2Int vector2Int = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
					int num3 = UnityEngine.Random.Range(6, 64);
					for (int m = 0; m < num3; m++)
					{
						for (int n = 0; n < 3; n++)
						{
							if (vector2Int.x + m < width && vector2Int.y + n < height)
							{
								worldBlocks[m + vector2Int.x, n + vector2Int.y] = 5;
							}
						}
					}
				}
				num2 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.35f, 0.5f);
				for (int num4 = 0; (float)num4 < num2; num4++)
				{
					Vector2Int vector2Int2 = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
					int num5 = UnityEngine.Random.Range(6, 60);
					for (int num6 = 0; num6 < num5; num6++)
					{
						for (int num7 = 0; num7 < 3; num7++)
						{
							if (vector2Int2.x + num7 < width && vector2Int2.y + num6 < height)
							{
								worldBlocks[num7 + vector2Int2.x, num6 + vector2Int2.y] = 5;
							}
						}
					}
				}
			}
			else
			{
				float num2 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.35f, 0.4f);
				for (int num8 = 0; (float)num8 < num2; num8++)
				{
					Vector2Int vector2Int3 = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
					int num9 = UnityEngine.Random.Range(6, 48);
					for (int num10 = 0; num10 < num9; num10++)
					{
						for (int num11 = 0; num11 < 2; num11++)
						{
							if (vector2Int3.x + num10 < width && vector2Int3.y + num11 < height)
							{
								worldBlocks[num10 + vector2Int3.x, num11 + vector2Int3.y] = 11;
							}
						}
					}
				}
				num2 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.35f, 0.4f);
				for (int num12 = 0; (float)num12 < num2; num12++)
				{
					Vector2Int vector2Int4 = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
					int num13 = UnityEngine.Random.Range(6, 48);
					for (int num14 = 0; num14 < num13; num14++)
					{
						for (int num15 = 0; num15 < 2; num15++)
						{
							if (vector2Int4.x + num15 < width && vector2Int4.y + num14 < height)
							{
								worldBlocks[num15 + vector2Int4.x, num14 + vector2Int4.y] = 11;
							}
						}
					}
				}
			}
			UpdateWorld();
			yield return WorldGenerateStructures();
			if (biomeDepth == 0)
			{
				PlaceLiquids(128f, 1, 32);
				yield break;
			}
			PlaceLiquids(10f, 1, 400);
			PlaceLiquids(18f, 2, 128);
		}
		else if (biomeDepth == 2 || biomeDepth == 3)
		{
			FastNoiseLite biomeMap = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			biomeMap.SetNoiseType(FastNoiseLite.NoiseType.Value);
			biomeMap.SetFrequency(0.086f);
			biomeMap.SetFractalType(FastNoiseLite.FractalType.FBm);
			biomeMap.SetFractalOctaves((biomeDepth == 2) ? 2 : 3);
			biomeMap.SetFractalGain(0.49f);
			biomeMap.SetFractalWeightedStrength(2.34f);
			biomeMap.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
			biomeMap.SetDomainWarpAmp(22f);
			FastNoiseLite frequencyMap = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			frequencyMap.SetFrequency(0.006f);
			FastNoiseLite dirtPerlinNoise = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			dirtPerlinNoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
			dirtPerlinNoise.SetFrequency(0.02f);
			dirtPerlinNoise.SetFractalType(FastNoiseLite.FractalType.Ridged);
			dirtPerlinNoise.SetFractalGain(0.65f);
			FastNoiseLite caveNoise = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			caveNoise.SetFrequency(0.005f);
			caveNoise.SetFractalType(FastNoiseLite.FractalType.PingPong);
			caveNoise.SetFractalGain(0.35f);
			caveNoise.SetDomainWarpType(FastNoiseLite.DomainWarpType.BasicGrid);
			caveNoise.SetDomainWarpAmp(40f);
			FastNoiseLite toxicNoise = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			toxicNoise.SetFrequency(0.012f);
			toxicNoise.SetFractalType(FastNoiseLite.FractalType.PingPong);
			toxicNoise.SetFractalGain(0.3f);
			toxicNoise.SetDomainWarpType(FastNoiseLite.DomainWarpType.BasicGrid);
			toxicNoise.SetDomainWarpAmp(50f);
			FastNoiseLite biomeMap2 = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			biomeMap2.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
			biomeMap2.SetFrequency(0.05f);
			biomeMap2.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.EuclideanSq);
			biomeMap2.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
			biomeMap2.SetCellularJitter(1f);
			biomeMap2.SetFractalType(FastNoiseLite.FractalType.Ridged);
			biomeMap2.SetFractalLacunarity(1.5f);
			FastNoiseLite marbleMap = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			marbleMap.SetFrequency((biomeDepth == 2) ? 0.007f : 0.035f);
			marbleMap.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
			marbleMap.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
			marbleMap.SetDomainWarpAmp(100f);
			int i = 0;
			float minMarble = ((biomeDepth == 2) ? 0.45f : 1f);
			for (int num16 = 0; num16 < width; num16++)
			{
				for (int num17 = 0; num17 < height; num17++)
				{
					float noise2 = biomeMap.GetNoise(num16, num17);
					float num18 = frequencyMap.GetNoise(num16, num17) * 0.25f + 0.1f;
					if (marbleMap.GetNoise(num16, num17) <= minMarble)
					{
						worldBlocks[num16, num17] = (ushort)((noise2 > num18 && dirtPerlinNoise.GetNoise(num16, num17) < -0.4f) ? ((noise2 < num18 + 0.1f) ? 12 : 13) : 0);
						if (caveNoise.GetNoise(num16, num17) > 0.65f)
						{
							worldBlocks[num16, num17] = 17;
						}
						if (noise2 > 0.75f)
						{
							worldBlocks[num16, num17] = 15;
						}
						if (biomeMap2.GetNoise(num16, num17) > 0.1f)
						{
							worldBlocks[num16, num17] = (ushort)UnityEngine.Random.Range(3, 5);
						}
						if (biomeDepth == 3 && worldBlocks[num16, num17] > 0 && UnityEngine.Random.value < 0.1f)
						{
							worldBlocks[num16, num17] = (ushort)(15 + UnityEngine.Random.Range(0, 2));
						}
					}
					else
					{
						i++;
						worldBlocks[num16, num17] = (ushort)((noise2 > num18) ? ((ushort)((dirtPerlinNoise.GetNoise(num16, num17) < -0.1f) ? 18u : 19u)) : 0);
						if (i > 100)
						{
							i = 0;
							UnityEngine.Object.Instantiate(Resources.Load("Special/marbleBackground"), BlockToWorldPos(new Vector2Int(num16, num17)), Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 360f));
						}
					}
					if (biomeDepth == 3 && toxicNoise.GetNoise(num16, num17) < -0.8f && UnityEngine.Random.value > 0.5f)
					{
						worldBlocks[num16, num17] = 22;
					}
					if (biomeDepth == 3 && worldBlocks[num16, num17] > 0 && UnityEngine.Random.value > (float)(num17 + halfHeight) / (float)height)
					{
						worldBlocks[num16, num17] = 23;
					}
				}
				tileCounter++;
				if (tileCounter > 100)
				{
					tileCounter = 0;
					yield return null;
				}
			}
			GenerateOres();
			float num2 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.25f, 0.3f);
			for (int num19 = 0; (float)num19 < num2; num19++)
			{
				Vector2Int vector2Int5 = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
				int num20 = UnityEngine.Random.Range(6, 48);
				for (int num21 = 0; num21 < num20; num21++)
				{
					for (int num22 = 0; num22 < 2; num22++)
					{
						if (vector2Int5.x + num21 < width && vector2Int5.y + num22 < height)
						{
							worldBlocks[num21 + vector2Int5.x, num22 + vector2Int5.y] = 11;
						}
					}
				}
			}
			num2 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.24f, 0.3f);
			for (int num23 = 0; (float)num23 < num2; num23++)
			{
				Vector2Int vector2Int6 = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
				int num24 = UnityEngine.Random.Range(6, 48);
				for (int num25 = 0; num25 < num24; num25++)
				{
					for (int num26 = 0; num26 < 2; num26++)
					{
						if (vector2Int6.x + num26 < width && vector2Int6.y + num25 < height)
						{
							worldBlocks[num26 + vector2Int6.x, num25 + vector2Int6.y] = 11;
						}
					}
				}
			}
			if (biomeDepth == 3)
			{
				num2 = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.5f, 0.6f);
				for (int num27 = 0; (float)num27 < num2; num27++)
				{
					Vector2Int vector2Int7 = new Vector2Int(UnityEngine.Random.Range(0, (int)width), UnityEngine.Random.Range(0, (int)height));
					int num28 = UnityEngine.Random.Range(4, 16);
					for (int num29 = 0; num29 < num28; num29++)
					{
						for (int num30 = 0; num30 < num28; num30++)
						{
							if (vector2Int7.x + num29 < width && vector2Int7.y + num30 < height)
							{
								worldBlocks[num29 + vector2Int7.x, num30 + vector2Int7.y] = 20;
							}
						}
					}
				}
			}
			UpdateWorld();
			yield return WorldGenerateStructures();
			PlaceLiquids(50f, 1, 26);
			PlaceLiquids(15f, 3, 128);
		}
		else
		{
			if (biomeDepth != 4)
			{
				yield break;
			}
			FastNoiseLite marbleMap = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			marbleMap.SetNoiseType(FastNoiseLite.NoiseType.Value);
			marbleMap.SetFractalType(FastNoiseLite.FractalType.Ridged);
			marbleMap.SetFractalOctaves(3);
			marbleMap.SetFractalLacunarity(2.29f);
			marbleMap.SetFractalGain(4f);
			marbleMap.SetFractalWeightedStrength(1.2f);
			marbleMap.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
			marbleMap.SetDomainWarpAmp(41f);
			FastNoiseLite biomeMap2 = new FastNoiseLite(UnityEngine.Random.Range(0, int.MaxValue));
			biomeMap2.SetFrequency(0.02f);
			biomeMap2.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
			biomeMap2.SetDomainWarpAmp(25f);
			for (int i = 0; i < width; i++)
			{
				for (int num31 = 0; num31 < height; num31++)
				{
					marbleMap.SetFrequency(0.0189f - (float)num31 / (float)height * 0.002f);
					float num32 = marbleMap.GetNoise(i, num31) + UnityEngine.Random.Range(-0.1f, 0.1f);
					if (num32 > 0.15f && num32 < 0.25f)
					{
						worldBlocks[i, num31] = 23;
					}
					else if (num32 >= 0.25f && num32 < 0.45f)
					{
						worldBlocks[i, num31] = 16;
					}
					else if (num32 >= 0.45f && num32 < 0.66f)
					{
						worldBlocks[i, num31] = 15;
					}
					else if (num32 >= 0.66f)
					{
						worldBlocks[i, num31] = 19;
					}
					if (biomeMap2.GetNoise(i, num31) < -0.735f)
					{
						worldBlocks[i, num31] = 0;
					}
				}
				tileCounter++;
				if (tileCounter > 100)
				{
					tileCounter = 0;
					yield return null;
				}
			}
			GenerateOres();
			UpdateWorld();
			yield return WorldGenerateStructures();
			PlaceLiquids(30f, 1, 128);
			PlaceLiquids(10f, 2, 50);
		}
	}

	public void DrawEmptyLine(in Vector2Int a, in Vector2Int b, int thickness)
	{
		int num = a.x;
		int num2 = a.y;
		int x = b.x;
		int y = b.y;
		int num3 = Math.Abs(x - num);
		int num4 = Math.Abs(y - num2);
		int num5 = ((num < x) ? 1 : (-1));
		int num6 = ((num2 < y) ? 1 : (-1));
		int num7 = num3 - num4;
		int num8 = thickness >> 1;
		bool flag = num3 >= num4;
		while (true)
		{
			if (flag)
			{
				for (int i = -num8; i <= num8; i++)
				{
					int num9 = num2 + i;
					if ((uint)num < width && (uint)num9 < height)
					{
						worldBlocks[num, num9] = 0;
					}
				}
			}
			else
			{
				for (int j = -num8; j <= num8; j++)
				{
					int num10 = num + j;
					if ((uint)num10 < width && (uint)num2 < height)
					{
						worldBlocks[num10, num2] = 0;
					}
				}
			}
			if (num != x || num2 != y)
			{
				int num11 = num7 << 1;
				if (num11 > -num4)
				{
					num7 -= num4;
					num += num5;
				}
				if (num11 < num3)
				{
					num7 += num3;
					num2 += num6;
				}
				continue;
			}
			break;
		}
	}

	public void DrawLine(in Vector2Int a, in Vector2Int b, int thickness, ushort block)
	{
		int num = a.x;
		int num2 = a.y;
		int x = b.x;
		int y = b.y;
		int num3 = Math.Abs(x - num);
		int num4 = Math.Abs(y - num2);
		int num5 = ((num < x) ? 1 : (-1));
		int num6 = ((num2 < y) ? 1 : (-1));
		int num7 = num3 - num4;
		int num8 = thickness >> 1;
		bool flag = num3 >= num4;
		while (true)
		{
			if (flag)
			{
				for (int i = -num8; i <= num8; i++)
				{
					int num9 = num2 + i;
					if ((uint)num < width && (uint)num9 < height)
					{
						worldBlocks[num, num9] = block;
					}
				}
			}
			else
			{
				for (int j = -num8; j <= num8; j++)
				{
					int num10 = num + j;
					if ((uint)num10 < width && (uint)num2 < height)
					{
						worldBlocks[num10, num2] = block;
					}
				}
			}
			if (num != x || num2 != y)
			{
				int num11 = num7 << 1;
				if (num11 > -num4)
				{
					num7 -= num4;
					num += num5;
				}
				if (num11 < num3)
				{
					num7 += num3;
					num2 += num6;
				}
				continue;
			}
			break;
		}
	}

	private IEnumerator WorldCreateBackground()
	{
		SetLoadingText("gencreatingbackground");
		yield return null;
		if (biomeOverride == OverrideSceneType.Tutorial)
		{
			Tilemap[,] array = chunks;
			foreach (Tilemap chunk in array)
			{
				CreateBackground("steelBackground", chunk);
			}
		}
		else if (biomeDepth <= 1)
		{
			Tilemap[,] array = chunks;
			foreach (Tilemap chunk2 in array)
			{
				CreateBackground((biomeDepth == 0) ? "rockBackground" : "soilBackground", chunk2);
			}
			if (biomeDepth == 0)
			{
				for (int k = 0; (float)k < (float)(chunkWidth * chunkHeight) * 0.4f; k++)
				{
					Vector2 vector = new Vector3(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, halfHeight));
					((GameObject)UnityEngine.Object.Instantiate(Resources.Load("Special/wallholes"), vector, Quaternion.Euler(new Vector3(0f, 0f, UnityEngine.Random.value * 360f)), GetClosestChunk(WorldToBlockPos(vector)).transform)).GetComponent<AudioSource>().pitch = UnityEngine.Random.Range(0.8f, 1.2f);
				}
			}
		}
		else if (biomeDepth == 2 || biomeDepth == 3)
		{
			Tilemap[,] array = chunks;
			foreach (Tilemap chunk3 in array)
			{
				CreateBackground((biomeDepth == 2) ? "sandBackground" : "wastelandBackground", chunk3);
			}
		}
		else
		{
			if (biomeDepth != 4)
			{
				yield break;
			}
			Tilemap[,] array = chunks;
			foreach (Tilemap chunk4 in array)
			{
				CreateBackground("grassBackground", chunk4);
			}
		}
	}

	private IEnumerator WorldPreprocess()
	{
		SetLoadingText("genpreprocessing");
		yield return null;
		PlayerCamera.main.body.transform.position = Vector3.zero;
		currentTempCurve = biomeDepth;
		UpdateBiomePostProcess();
		if (biomeOverride == OverrideSceneType.None)
		{
			ResetLayerModifiers();
		}
		PlayerCamera.main.backgroundSnow.SetActive(biomeDepth == 5);
	}

	public void DistributeMiniBarrels()
	{
		float num = (float)(chunkWidth * chunkHeight) * UnityEngine.Random.Range(0.18f, 0.2f) * lootRarityMultiplier;
		for (int i = 0; (float)i < num; i++)
		{
			Vector2 vector = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfHeight, PlayerCamera.main.body.transform.position.y - 5f));
			if ((bool)Physics2D.OverlapPoint(vector, LayerMask.GetMask("Ground")))
			{
				continue;
			}
			RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, CHUNKSIZE, LayerMask.GetMask("Ground"));
			if ((bool)raycastHit2D)
			{
				GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("minibarrel"), raycastHit2D.point + Vector2.up, Quaternion.identity);
				int liquidCount = Mathf.RoundToInt(UnityEngine.Random.value * UnityEngine.Random.value * 8f);
				if (liquidCount < 1)
				{
					liquidCount = 1;
				}
				for (int j = 0; j < liquidCount; j++)
				{
					string key = Liquids.Registry.ElementAt(UnityEngine.Random.Range(0, Liquids.Registry.Count)).Key;
					gameObject.GetComponent<WaterContainerItem>().AddLiquid(key, UnityEngine.Random.value * 10000f / (float)liquidCount * UnityEngine.Random.value);
				}
			}
		}
	}

	private void GenerateCollapsedPods(float amt)
	{
		float num = (float)(chunkWidth * chunkHeight) * amt * totalLootRarity;
		for (int i = 0; (float)i < num; i++)
		{
			Vector2 vector = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
			RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, 400f, LayerMask.GetMask("Ground"));
			if ((bool)raycastHit2D)
			{
				vector = raycastHit2D.point;
			}
			Vector2Int pos = WorldToBlockPos(vector);
			for (int j = 0; j < 90; j++)
			{
				for (int k = 0; k < 90; k++)
				{
					float num2 = Vector2.Distance(vector + Vector2.up * k + Vector2.right * j - Vector2.one * 45f, vector);
					if (num2 < 45f * UnityEngine.Random.Range(0f, 12f / (num2 * 0.8f)) && UnityEngine.Random.Range(0f, 1f) < 0.7f)
					{
						Vector2Int vector2Int = WorldToBlockPos(vector);
						vector2Int.x += -45 + j;
						vector2Int.y += -45 + k + 2;
						if (vector2Int.x < 0)
						{
							vector2Int.x = 0;
						}
						if (vector2Int.x > width - 1)
						{
							vector2Int.x = (int)(width - 1);
						}
						if (vector2Int.y < 0)
						{
							vector2Int.y = 0;
						}
						if (vector2Int.y > height - 1)
						{
							vector2Int.y = (int)(height - 1);
						}
						if (worldBlocks[vector2Int.x, vector2Int.y] > 0)
						{
							worldBlocks[vector2Int.x, vector2Int.y] = (ushort)UnityEngine.Random.Range(0, 5);
						}
					}
				}
			}
			GenerateObjectAtPos(pos, Resources.Load<GameObject>("LifepodCollapsed").transform.GetChild(0).GetComponent<Tilemap>(), 0.88f, genMode: true);
			if (UnityEngine.Random.value < 0.9f)
			{
				AmmoScript component = (UnityEngine.Object.Instantiate(Resources.Load(spawnableMagazines.PickRandom()), vector, Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 360f)) as GameObject).GetComponent<AmmoScript>();
				component.rounds = Mathf.RoundToInt((float)component.maxRounds * UnityEngine.Random.value);
			}
			for (int l = 0; l < 3; l++)
			{
				if (UnityEngine.Random.Range(0f, 1f) < 0.3f)
				{
					UnityEngine.Object.Instantiate(Resources.Load("experimentflesh"), vector + Vector2.right * UnityEngine.Random.Range(-3f, 3f), Quaternion.identity);
				}
			}
			if (UnityEngine.Random.Range(0f, 1f) < 0.8f)
			{
				UnityEngine.Object.Instantiate(Resources.Load("internalorgans"), vector + Vector2.right * UnityEngine.Random.Range(-3f, 3f), Quaternion.identity);
			}
		}
	}

	private void GenerateLifePods(float amt)
	{
		float num = (float)(chunkWidth * chunkHeight) * amt * totalLootRarity;
		for (int i = 0; (float)i < num; i++)
		{
			Vector2 vector = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32), UnityEngine.Random.Range(0L - (long)halfWidth + 32, halfWidth - 32));
			RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, 400f, LayerMask.GetMask("Ground"));
			if ((bool)raycastHit2D)
			{
				vector = raycastHit2D.point;
			}
			Vector2Int vector2Int = WorldToBlockPos(vector);
			for (int j = 0; j < 90; j++)
			{
				for (int k = 0; k < 90; k++)
				{
					float num2 = Vector2.Distance(vector + Vector2.up * k + Vector2.right * j - Vector2.one * 45f, vector);
					if (num2 < 45f * UnityEngine.Random.Range(0f, 12f / (num2 * 0.8f)) && UnityEngine.Random.Range(0f, 1f) < 0.7f)
					{
						Vector2Int vector2Int2 = WorldToBlockPos(vector);
						vector2Int2.x += -45 + j;
						vector2Int2.y += -45 + k + 2;
						if (vector2Int2.x < 0)
						{
							vector2Int2.x = 0;
						}
						if (vector2Int2.x > width - 1)
						{
							vector2Int2.x = (int)(width - 1);
						}
						if (vector2Int2.y < 0)
						{
							vector2Int2.y = 0;
						}
						if (vector2Int2.y > height - 1)
						{
							vector2Int2.y = (int)(height - 1);
						}
						if (worldBlocks[vector2Int2.x, vector2Int2.y] > 0)
						{
							worldBlocks[vector2Int2.x, vector2Int2.y] = (ushort)UnityEngine.Random.Range(0, 5);
						}
					}
				}
			}
			GenerateObjectAtPos(vector2Int, Resources.Load<GameObject>("Lifepod").transform.GetChild(0).GetComponent<Tilemap>(), 0.95f, genMode: true);
			GenerateEntityAtPos(BlockToWorldPos(vector2Int), Resources.Load<GameObject>("Lifepod"));
			if (UnityEngine.Random.value < GetRunSettingFloat("traderchance") * 0.01f)
			{
				int num3 = UnityEngine.Random.Range(-4, 4);
				TraderScript component = (UnityEngine.Object.Instantiate(Resources.Load("trader" + UnityEngine.Random.Range(1, 4)), BlockToWorldPos(vector2Int + Vector2Int.down * 7 + Vector2Int.right * num3) - Vector2.one * 0.5f, Quaternion.identity) as GameObject).GetComponent<TraderScript>();
				if ((float)Mathf.Abs(num3) > 1.5f)
				{
					component.farEnoughToMove = true;
				}
				component.MoveRange = new RangeF(BlockToWorldPos(vector2Int - Vector2Int.right * 5).x, BlockToWorldPos(vector2Int + Vector2Int.right * 5).x);
			}
			else
			{
				UnityEngine.Object.Instantiate(Resources.Load("lifepodchest"), BlockToWorldPos(vector2Int + Vector2Int.down * 6) - Vector2.one * 0.5f, Quaternion.identity);
			}
			for (int l = 0; l < 3; l++)
			{
				if (UnityEngine.Random.Range(0f, 1f) < 0.05f)
				{
					UnityEngine.Object.Instantiate(Resources.Load("experimentflesh"), vector + Vector2.right * UnityEngine.Random.Range(-3f, 3f), Quaternion.identity);
				}
			}
			if (UnityEngine.Random.Range(0f, 1f) < 0.05f)
			{
				UnityEngine.Object.Instantiate(Resources.Load("internalorgans"), vector + Vector2.right * UnityEngine.Random.Range(-3f, 3f), Quaternion.identity);
			}
			if (UnityEngine.Random.Range(0f, 1f) < 0.5f)
			{
				UnityEngine.Object.Instantiate(Resources.Load("LoreNote"), vector + Vector2.right * UnityEngine.Random.Range(-3f, 3f) + Vector2.up * UnityEngine.Random.Range(-1f, -6f), Quaternion.identity);
			}
			if (UnityEngine.Random.Range(0f, 1f) < 0.285f)
			{
				Utils.Create("epda", vector + Vector2.right * UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.value * 360f);
			}
			if (UnityEngine.Random.value < 0.2f)
			{
				Vector2 pos = vector + Vector2.right * UnityEngine.Random.Range(-1.5f, 1.5f);
				Utils.Create("Special/defibrack", pos, 0f);
				bool num4 = UnityEngine.Random.value < 0.5f;
				float value = UnityEngine.Random.value;
				GameObject gameObject = null;
				if (UnityEngine.Random.value < 0.75f)
				{
					gameObject = Utils.Create("manualdefibrillator", pos, 0f);
					gameObject.AddComponent<ItemLock>();
				}
				else
				{
					gameObject = Utils.Create("aed", pos, 0f);
					gameObject.AddComponent<ItemLock>();
				}
				if (!num4)
				{
					gameObject.GetComponent<Item>().battery.UnloadBattery(skipSpawn: true);
				}
				else
				{
					gameObject.GetComponent<Item>().condition = value;
				}
			}
		}
	}

	private void GenerateDropCapsules(float amt)
	{
		float num = (float)(chunkWidth * chunkHeight) * amt * totalLootRarity;
		for (int i = 0; (float)i < num; i++)
		{
			Vector2 vector = new Vector2(UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth), UnityEngine.Random.Range(0L - (long)halfWidth, halfWidth));
			RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, 400f, LayerMask.GetMask("Ground"));
			if ((bool)raycastHit2D)
			{
				vector = raycastHit2D.point;
			}
			((GameObject)UnityEngine.Object.Instantiate(Resources.Load("dropcapsule"), vector, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)))).GetComponent<AudioSource>().pitch = UnityEngine.Random.Range(0.9f, 1.1f);
			GenerateBlockCircle(vector, 32, 3, 0.7f, 0f);
			GenerateBlockCircle(vector, 30, 6, 0.04f, 0.04f);
			GenerateBlockCircle(vector, 4, 0, 1f, 0.9f);
		}
	}

	public void GenerateTree(Vector2Int pos)
	{
		int num = UnityEngine.Random.Range(12, 60);
		int num2 = UnityEngine.Random.Range(1, 9);
		for (int i = 0; i < num; i++)
		{
			if (UnityEngine.Random.value < 0.17f)
			{
				num2 += ((!(UnityEngine.Random.value < 0.5f)) ? 1 : (-1));
			}
			if (UnityEngine.Random.value < 0.5f)
			{
				pos.x += ((!(UnityEngine.Random.value < 0.5f)) ? 1 : (-1));
			}
			if (pos.x < 0)
			{
				pos.x = 0;
			}
			if (num2 < 1)
			{
				num2 = 1;
			}
			if (num2 > 11)
			{
				num2 = 9;
			}
			for (int j = 0; j < num2; j++)
			{
				if (pos.x + j < width - 1 && pos.y < height - 1)
				{
					worldBlocks[pos.x + j, pos.y] = 24;
					if ((num2 > 2 && j == Mathf.FloorToInt((float)num2 / 2f)) || (num2 > 3 && j == Mathf.CeilToInt((float)num2 / 2f)))
					{
						worldBlocks[pos.x + j, pos.y] = 0;
						FluidManager.main.fluid[pos.x + j, pos.y] = 4;
					}
				}
			}
			if (UnityEngine.Random.value < 0.95f)
			{
				pos.y++;
			}
		}
		for (int k = 0; k < UnityEngine.Random.Range(10, 20); k++)
		{
			int num3 = UnityEngine.Random.Range(4, 8);
			GenerateBlockCircle(BlockToWorldPos(pos) + Vector2.up * num3 + Vector2.right * UnityEngine.Random.Range(-10f, 10f) + Vector2.up * UnityEngine.Random.Range(-10f, 10f), num3, 25, 0.9f, 0.5f, autoUpdateChunk: false, force: true);
		}
	}

	public void GenerateBigMushroom(Vector2Int pos)
	{
		Vector2 vector = pos;
		int num = UnityEngine.Random.Range(2, 5);
		int num2 = UnityEngine.Random.Range(15, 85);
		Vector2 normalized = UnityEngine.Random.insideUnitCircle.normalized;
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				for (int k = 0; k < num; k++)
				{
					Vector2 vector2 = vector + Vector2.right * j + Vector2.up * k;
					SetBlockNoUpdate(new Vector2Int((int)vector2.x, (int)vector2.y), 32);
				}
			}
			vector += normalized;
			if (i % 2 == 0)
			{
				normalized += UnityEngine.Random.insideUnitCircle * 0.3f;
				normalized.Normalize();
			}
		}
		for (int l = 0; l < UnityEngine.Random.Range(10, 20); l++)
		{
			int num3 = UnityEngine.Random.Range(4, 8);
			GenerateBlockCircle(BlockToWorldPos(pos) + Vector2.up * num3 + Vector2.right * UnityEngine.Random.Range(-6f, 6f) + Vector2.up * UnityEngine.Random.Range(-6f, 6f), num3, 33, 1f, 1f, autoUpdateChunk: false, force: true);
		}
	}

	private IEnumerator FinishWorldGeneration()
	{
		layerTimeSpent = 0f;
		RadiationLine.line.Deactivate();
		if (doPod)
		{
			doPod = false;
			PlayerCamera.main.body.hearingLoss += 15f;
			PlayerCamera.main.body.hunger -= 10f;
			PlayerCamera.main.body.thirst -= 15f;
			PlayerCamera.main.body.talker.Talk(Locale.GetCharacter("drillend"));
			UnityEngine.Object.Instantiate(Resources.Load("drillpodbroken"), PlayerCamera.main.body.transform.position + Vector3.down * 2f, Quaternion.Euler(new Vector3(0f, 0f, UnityEngine.Random.Range(-10f, 10f))));
		}
		GlobalDark.main.Darken();
		if (biomeOverride == OverrideSceneType.None)
		{
			DistributeMiniBarrels();
		}
		yield return new WaitUntil(() => !GlobalDark.main.IsDarkening());
		if (biomeOverride == OverrideSceneType.None)
		{
			ApplyLayerModifiers();
		}
		generatingWorld = false;
		caveAudio.Stop();
		caveAudio.clip = backgroundDrones[biomeDepth];
		caveAudio.Play();
		DisableAllChunks();
		UpdateChunkVisibility();
		timeSinceFinishedGeneration = 0f;
		loadingObject.SetActive(value: false);
		if (biomeOverride == OverrideSceneType.None)
		{
			string text = Locale.GetOther("layer") + " " + (biomeDepth + 1) + "\n" + biomeTitles[biomeDepth];
			if (!string.IsNullOrEmpty(layerPrefix))
			{
				string text2 = "<color=\"orange\">" + layerPrefix + "</color> ";
				text = Locale.GetOther("layer") + " " + (biomeDepth + 1) + "\n" + text2 + biomeTitles[biomeDepth];
				string text3 = "<color=\"orange\">" + layerDescription + "</color>";
				PlayerCamera.main.StartCoroutine(PlayerCamera.main.DoAlertDelayed(text3, important: false, 6f));
				layerPrefix = null;
				layerDescription = null;
			}
			PlayerCamera.main.DoAlert(text, important: true);
		}
		else if (biomeOverride == OverrideSceneType.Tutorial)
		{
			PlayerCamera.main.DoAlert(Locale.GetOther("layertitletutorial"), important: true);
		}
		if (biomeDepth == 0 && biomeOverride == OverrideSceneType.None)
		{
			Sound.Play("lifePodHit", PlayerCamera.main.body.transform.position, twoDimensional: true, pitchShift: false);
			PlayerCamera.main.Invoke("LifePodShake", 1f);
			UnityEngine.Object.Instantiate(Resources.Load("Special/ExplosionParticle"), PlayerCamera.main.body.transform.position + Vector3.down * 11f, Quaternion.identity);
		}
		if (biomeDepth == 5)
		{
			SetFog(UnityEngine.Random.Range(0.8f, 1f));
		}
	}

	private void GenerateOres()
	{
		float runSettingFloat = GetRunSettingFloat("oreamount");
		for (int num = Mathf.RoundToInt((float)((int)(chunkWidth * chunkHeight) / 2) * runSettingFloat); num > 0; num--)
		{
			Vector2Int vector2Int = new Vector2Int((int)UnityEngine.Random.Range(0f, width), (int)UnityEngine.Random.Range(0f, height));
			for (int num2 = UnityEngine.Random.Range(1, 26); num2 > 0; num2--)
			{
				if (vector2Int.x > 0 && vector2Int.x < width - 1 && vector2Int.y > 0 && vector2Int.y < height - 1 && worldBlocks[vector2Int.x, vector2Int.y] > 0)
				{
					worldBlocks[vector2Int.x, vector2Int.y] = 34;
				}
				vector2Int += new Vector2Int((UnityEngine.Random.value > 0.5f) ? ((UnityEngine.Random.value > 0.5f) ? 1 : (-1)) : 0, (UnityEngine.Random.value > 0.5f) ? ((UnityEngine.Random.value > 0.5f) ? 1 : (-1)) : 0);
			}
		}
		if (biomeDepth < 4)
		{
			return;
		}
		int num3 = 0;
		int num4 = Mathf.RoundToInt(1024f / Mathf.Clamp(runSettingFloat, 0.01f, 999f));
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				num3++;
				if (num3 > num4 && UnityEngine.Random.value < 0.001f)
				{
					if (worldBlocks[i, j] > 0)
					{
						worldBlocks[i, j] = 35;
					}
					num3 = 0;
				}
			}
		}
	}

	public void ApplyLayerModifiers()
	{
		if (UnityEngine.Random.value < GetRunSettingFloat("layermodifierchance") * 0.01f)
		{
			LayerModifier layerModifier = ((biomeDepth > 1) ? LayerModifier.availableModifiers.PickRandom() : LayerModifier.availableModifiers.Where((LayerModifier x) => !x.hideOnFirstLayer).ToList().PickRandom());
			layerModifier.Initialize(this);
			layerModifier.active = true;
			layerPrefix = Locale.GetOther("layermodifier" + layerModifier.modifierIndex);
			layerDescription = Locale.GetOther("layermodifier" + layerModifier.modifierIndex + "dsc");
		}
	}

	public void ResetLayerModifiers()
	{
		layerPrefix = "";
		layerDescription = "";
		LayerModifier[] availableModifiers = LayerModifier.availableModifiers;
		foreach (LayerModifier layerModifier in availableModifiers)
		{
			if (layerModifier.active)
			{
				layerModifier.Disable(this);
				layerModifier.active = false;
			}
		}
	}

	public void SetFog(float fog)
	{
		fogAmount = fog;
		fogSprite.color = new Color(0.8f, 0.8f, 0.8f, fogAmount);
	}

	public void UpdateBiomePostProcess()
	{
		if (biomeOverride == OverrideSceneType.None)
		{
			PlayerCamera.main.volume.profile = biomeProfiles[biomeDepth];
		}
		else if (biomeOverride == OverrideSceneType.Tutorial)
		{
			PlayerCamera.main.volume.profile = tutorialProfile;
		}
		if (PlayerCamera.main.volume.profile.TryGet<FilmGrain>(out var component))
		{
			component.intensity.value = biomeProfileNoise[biomeDepth] * Settings.Get<SettingFloat>("filmgrain").value;
		}
		if (PlayerCamera.main.volume.profile.TryGet<Bloom>(out var component2))
		{
			component2.active = Settings.Get<SettingBool>("bloom").value;
		}
	}

	private IEnumerator InstantiateWorld(bool generate)
	{
		loadingObject.SetActive(value: true);
		SetLoadingText("geninstantiatingchunks");
		instantiatingWorld = true;
		generatingWorld = true;
		width = chunkWidth * (uint)CHUNKSIZE;
		height = chunkHeight * (uint)CHUNKSIZE;
		FluidManager.main.fluid = new byte[width, height];
		worldBlocks = new ushort[width, height];
		chunks = new Tilemap[chunkWidth, chunkHeight];
		renderChunks = new TilemapRenderer[chunkWidth, chunkHeight];
		ChunkUpdated = new UnityEvent[chunkWidth, chunkHeight];
		chunkScripts = new ChunkScript[chunkWidth, chunkHeight];
		backgrounds.Clear();
		RadiationLine.line.Deactivate();
		foreach (BlockDamage blockDamage in blockDamages)
		{
			blockDamage.DestroySprite();
		}
		blockDamages.Clear();
		yield return null;
		for (int w = 0; w < chunkWidth; w++)
		{
			for (int i = 0; i < chunkHeight; i++)
			{
				GameObject gameObject = new GameObject("Chunk", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
				gameObject.isStatic = true;
				gameObject.layer = 6;
				gameObject.tag = "BlockGround";
				Tilemap component = gameObject.GetComponent<Tilemap>();
				TilemapRenderer component2 = gameObject.GetComponent<TilemapRenderer>();
				TilemapCollider2D component3 = gameObject.GetComponent<TilemapCollider2D>();
				component3.extrusionFactor = 0.0005f;
				component3.useDelaunayMesh = false;
				component3.maximumTileChangeCount = 99999u;
				gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
				gameObject.AddComponent<CompositeCollider2D>();
				component2.detectChunkCullingBounds = TilemapRenderer.DetectChunkCullingBounds.Manual;
				component2.chunkCullingBounds = Vector3.zero;
				component2.material = defaultMat;
				component2.sortingOrder = 1000;
				chunks[w, i] = component;
				renderChunks[w, i] = component2;
				gameObject.transform.position = new Vector2(((float)w - (float)chunkWidth * 0.5f) * (float)CHUNKSIZE + (float)HALFCHUNKSIZE, ((float)i - (float)chunkHeight * 0.5f) * (float)CHUNKSIZE + (float)HALFCHUNKSIZE);
				gameObject.transform.SetParent(worldGrid.transform);
				ChunkUpdated[w, i] = new UnityEvent();
				component2.enabled = false;
				gameObject.AddComponent<ChunkScript>().pos = new Vector2Int(w, i);
				chunkScripts[w, i] = gameObject.GetComponent<ChunkScript>();
			}
			yield return 0;
		}
		generatingWorld = false;
		instantiatingWorld = false;
		loadingObject.SetActive(value: false);
		UpdateChunkVisibility();
		if (generate)
		{
			StartCoroutine(GenerateWorld());
		}
		else
		{
			loadingObject.SetActive(value: false);
		}
	}

	public Vector2 BlockToWorldPos(Vector2Int pos)
	{
		return new Vector2((float)pos.x - (float)halfWidth + 0.5f, (float)pos.y - (float)halfHeight + 0.5f);
	}

	public static void CreateDamageNumber(Vector2 pos, int num)
	{
		GameObject obj = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("DamageText"), pos, Quaternion.identity);
		obj.GetComponent<TextMeshPro>().text = num.ToString();
		obj.GetComponent<TextMeshPro>().color = world.structureDamageGrad.Evaluate((float)num * 0.01f);
		UnityEngine.Object.Destroy(obj, 0.5f);
	}

	public bool Linecast(Vector2 startWorld, Vector2 endWorld, out Vector2 hitEntryPosition, out float distance)
	{
		hitEntryPosition = Vector2.zero;
		distance = 0f;
		Vector2 normalized = (endWorld - startWorld).normalized;
		Vector2 vector = new Vector2(startWorld.x + (float)halfWidth, startWorld.y + (float)halfHeight);
		new Vector2(endWorld.x + (float)halfWidth, endWorld.y + (float)halfHeight);
		int num = Mathf.FloorToInt(vector.x);
		int num2 = Mathf.FloorToInt(vector.y);
		int num3 = ((normalized.x > 0f) ? 1 : (-1));
		int num4 = ((normalized.y > 0f) ? 1 : (-1));
		float num5 = ((normalized.x == 0f) ? float.MaxValue : Mathf.Abs(1f / normalized.x));
		float num6 = ((normalized.y == 0f) ? float.MaxValue : Mathf.Abs(1f / normalized.y));
		float num7 = ((normalized.x > 0f) ? ((Mathf.Floor(vector.x + 1f) - vector.x) * num5) : ((vector.x - Mathf.Floor(vector.x)) * num5));
		float num8 = ((normalized.y > 0f) ? ((Mathf.Floor(vector.y + 1f) - vector.y) * num6) : ((vector.y - Mathf.Floor(vector.y)) * num6));
		float num9 = 0f;
		while (num >= 0 && num2 >= 0 && num < width && num2 < height)
		{
			int num10 = worldBlocks[num, num2];
			if (num10 > 0 && num10 != 7)
			{
				bool flag = false;
				int[,] array = new int[4, 2]
				{
					{ 1, 0 },
					{ -1, 0 },
					{ 0, 1 },
					{ 0, -1 }
				};
				for (int i = 0; i < 4; i++)
				{
					int num11 = num + array[i, 0];
					int num12 = num2 + array[i, 1];
					if (num11 >= 0 && num12 >= 0 && num11 < width && num12 < height)
					{
						int num13 = worldBlocks[num11, num12];
						if (num13 == 0 || num13 == 7)
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					Vector2 vector2 = vector + normalized * num9;
					hitEntryPosition = new Vector2(vector2.x - (float)halfWidth, vector2.y - (float)halfHeight);
					distance = Vector2.Distance(startWorld, hitEntryPosition);
					return true;
				}
			}
			if (num7 < num8)
			{
				num9 = num7;
				num7 += num5;
				num += num3;
			}
			else
			{
				num9 = num8;
				num8 += num6;
				num2 += num4;
			}
			float num14 = Vector2.Distance(startWorld, endWorld);
			if (num9 > num14)
			{
				break;
			}
		}
		return false;
	}

	public static void CreateExplosion(ExplosionParams param)
	{
		Sound.Play(param.sound, Vector2.zero, twoDimensional: true, pitchShift: false);
		UnityEngine.Object.Instantiate(Resources.Load("Special/ExplosionParticle"), param.position, Quaternion.identity);
		GameObject obj = UnityEngine.Object.Instantiate(Resources.Load("Special/blastmark"), world.GetClosestChunk(world.WorldToBlockPos(param.position)).transform) as GameObject;
		obj.transform.position = param.position;
		obj.transform.eulerAngles = new Vector3(0f, 0f, UnityEngine.Random.value * 360f);
		PlayerCamera.main.shaker.Shake(param.range * 20f);
		if (Vector2.Distance(param.position, PlayerCamera.main.body.transform.position) < param.range * 2.5f)
		{
			Sound.Play("tinnitus", Vector2.zero, twoDimensional: true, pitchShift: false, null, 1f, 1f, noReverb: true, ignoreMixer: true);
			PlayerCamera.main.body.eyePanicTime = 1f;
			PlayerCamera.main.body.eyeCloseTime = 5f;
			PlayerCamera.main.body.eyeScareTime = 12f;
			PlayerCamera.main.body.consciousness = 31f;
			PlayerCamera.main.body.hearingLoss += UnityEngine.Random.Range(27f, 36.6f);
			PlayerCamera.main.body.talker.Talk(Locale.GetCharacter("loud"));
			PlayerCamera.main.shaker.Shake(param.range * 20f);
		}
		Collider2D[] array = Physics2D.OverlapCircleAll(param.position, param.range);
		List<Limb> list = new List<Limb>();
		Collider2D[] array2 = array;
		foreach (Collider2D obj2 in array2)
		{
			if (obj2.TryGetComponent<BuildingEntity>(out var component))
			{
				component.health -= param.structuralDamage * UnityEngine.Random.Range(0f, 2f);
				if (component.TryGetComponent<Rigidbody2D>(out var component2))
				{
					component2.velocity = ((Vector2)component.transform.position - param.position).normalized * param.velocity;
				}
			}
			if (obj2.TryGetComponent<Item>(out var component3))
			{
				component3.SetCondition(component3.condition - param.structuralDamage * 0.005f * UnityEngine.Random.Range(0f, 1.3f));
				if (component3.TryGetComponent<Rigidbody2D>(out var component4))
				{
					component4.velocity = ((Vector2)component3.transform.position - param.position).normalized * param.velocity;
				}
			}
			if (obj2.TryGetComponent<Limb>(out var component5))
			{
				list.Add(component5);
			}
			if (obj2.TryGetComponent<Body>(out var component6))
			{
				list.AddRange(component6.limbs);
			}
		}
		world.GenerateBlockCircle(param.position, (int)param.range, 0, 1f, 0.85f, autoUpdateChunk: true, force: false, ignoreInfinirock: true);
		foreach (Limb item in list)
		{
			if ((bool)Physics2D.Linecast(param.position, item.transform.position, LayerMask.GetMask("Ground")))
			{
				continue;
			}
			float armorReduction = item.GetArmorReduction();
			if (UnityEngine.Random.Range(0f, 1f) < param.skinDamageChance)
			{
				item.skinHealth -= param.skinDamage.RandomFromRange() / armorReduction;
			}
			item.muscleHealth -= param.muscleDamage.RandomFromRange() / armorReduction;
			item.body.shock = 100f;
			item.body.lastTimeStepVelocity = ((Vector2)item.body.transform.position - param.position).normalized * param.velocity;
			item.body.Ragdoll();
			if (!item.hasShrapnel)
			{
				item.shrapnel = ((UnityEngine.Random.value < param.shrapnelChance) ? 5 : 0);
			}
			item.DamageWearables(param.shrapnelChance);
			if (item.isVital && UnityEngine.Random.value < 0.5f)
			{
				item.body.internalBleeding += param.muscleDamage.RandomFromRange() * 0.4f / armorReduction;
			}
			if (UnityEngine.Random.Range(0f, 1f) < param.bleedChance)
			{
				item.bleedAmount += param.bleedAmount.RandomFromRange() / armorReduction;
			}
			if (UnityEngine.Random.Range(0f, 1f) < param.boneBreakChance / armorReduction)
			{
				item.BreakBone();
			}
			if (UnityEngine.Random.Range(0f, 1f) < param.dislocationChance / armorReduction)
			{
				item.Dislocate();
			}
			if (item.isHead)
			{
				item.body.consciousness = 0f;
				if (UnityEngine.Random.Range(0f, 1f) < 0.7f / armorReduction)
				{
					item.body.brainHealth -= param.muscleDamage.RandomFromRange() / armorReduction * 0.5f;
				}
				if (UnityEngine.Random.Range(0f, 1f) < param.disfigureChance / armorReduction)
				{
					item.body.Disfigure();
				}
				if (UnityEngine.Random.Range(0f, 1f) < param.disfigureChance / armorReduction)
				{
					item.body.RemoveEye();
				}
			}
		}
	}
}

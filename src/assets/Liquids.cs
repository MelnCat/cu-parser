using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class Liquids
{
	public static List<string> DangerList = new List<string>
	{
		"mindwipe", "bleach", "braingrow", "fentanyl", "mercury", "sleepingpills", "mold", "morphine", "painkillers", "antidepressants",
		"oxyline", "heroin"
	};

	public static Dictionary<string, LiquidType> Registry = new Dictionary<string, LiquidType>
	{
		["water"] = new LiquidType
		{
			localeName = "cleanwater",
			color = new Color32(117, 209, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 14f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 90f);
				body.temperature -= num * 2.5f;
			},
			injectionSickness = 0f,
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 1f)
			}
		},
		["carbonatedwater"] = new LiquidType
		{
			localeName = "carbonatedwater",
			color = new Color32(102, 166, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 17f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 90f);
				body.Burp();
				body.happiness += num * 8f;
				body.temperature -= num * 2.5f;
			},
			injectionSickness = 0f,
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 1f)
			}
		},
		["lrdserum"] = new LiquidType
		{
			localeName = "lrdserum",
			color = new Color32(212, 203, 135, byte.MaxValue),
			valuePerLiter = 120f,
			injectionSickness = 0f,
			onDrink = delegate(float ml, Body body)
			{
				body.sicknessAmount += ml / 1000f * 50f;
				body.talker.EatBad();
			}
		},
		["morphine"] = new LiquidType
		{
			localeName = "morphine",
			color = new Color(0.588f, 0.482f, 0.373f),
			valuePerLiter = 110f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.01f;
				body.GetOrAddComponent<Painkillers>().opiateAmount += num * 40f;
			},
			injectionSickness = 0f,
			injectable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				limb.body.GetOrAddComponent<Painkillers>().opiateAmount += num * 90f;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("opiate", 1f)
			}
		},
		["biochem"] = new LiquidType
		{
			localeName = "biochem",
			color = new Color(0.702f, 1f, 0.145f),
			valuePerLiter = 45f,
			onDrink = delegate(float ml, Body body)
			{
				float hundredML = ml * 0.01f;
				body.sicknessAmount += hundredML * 40f;
				body.happiness -= hundredML * 10f;
				body.talker.EatBad();
				CoUtils.instance.DoTimedOp("biochem", delegate
				{
					body.limbs[0].pain += hundredML * 5f;
					body.limbs[1].pain += hundredML * 5f;
					body.limbs[0].muscleHealth -= hundredML * 2.5f;
					body.limbs[1].muscleHealth -= hundredML * 3f;
				}, 10f);
				body.limbs[0].SetDisinfect(Mathf.Clamp01(hundredML) * 200f);
			},
			injectable = true,
			injectionSickness = 2f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				limb.SetDisinfect(num * 30f);
				limb.body.sicknessAmount += num * 100f;
				Limb[] limbs = limb.body.limbs;
				foreach (Limb obj in limbs)
				{
					obj.muscleHealth -= num * 10f;
					obj.pain += num * 40f;
				}
			}
		},
		["opium"] = new LiquidType
		{
			localeName = "opium",
			color = new Color(1f, 0.922f, 0.318f),
			valuePerLiter = 100f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.01f;
				body.GetOrAddComponent<Painkillers>().opiateAmount += num * 20f;
			},
			injectionSickness = 0f,
			injectable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				limb.body.GetOrAddComponent<Painkillers>().opiateAmount += num * 40f;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("opiate", 0.4f)
			}
		},
		["painkillers"] = new LiquidType
		{
			localeName = "painkillers",
			color = new Color(1f, 1f, 1f),
			valuePerLiter = 125f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.01f;
				body.GetOrAddComponent<Painkillers>().opiateAmount += num * 140f;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("opiate", 1f)
			}
		},
		["heroin"] = new LiquidType
		{
			localeName = "heroin",
			color = new Color(0.92f, 0.92f, 0.92f),
			valuePerLiter = 140f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.01f;
				body.GetOrAddComponent<Painkillers>().opiateAmount += num * 60f;
				body.sicknessAmount += num * 25f;
			},
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				limb.body.GetOrAddComponent<Painkillers>().opiateAmount += num * 130f;
				limb.body.sicknessAmount += num * 50f;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("opiate", 1.5f)
			}
		},
		["naloxone"] = new LiquidType
		{
			localeName = "naloxone",
			color = new Color(0.91f, 0.847f, 0.745f),
			valuePerLiter = 80f,
			onDrink = delegate
			{
			},
			injectionSickness = 0f,
			injectable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				if (limb.body.TryGetComponent<Painkillers>(out var component))
				{
					component.antagonistAmount += num * 50f;
				}
			}
		},
		["naltrexone"] = new LiquidType
		{
			localeName = "naltrexone",
			color = new Color(1f, 1f, 1f),
			valuePerLiter = 100f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.05f;
				CoUtils.instance.DoTimedOp("naltrexone", delegate
				{
					body.sicknessAmount -= 1f;
				}, 25f * num);
				if (Random.Range(0f, 1f) < 0.15f * num)
				{
					body.vomiter.Vomit();
				}
				body.happiness -= 2f * num;
				if (body.TryGetComponent<Painkillers>(out var component))
				{
					component.antagonistAmount += 15f * num;
					component.opiateTolerance -= 15f * num;
				}
			}
		},
		["ceftriaxone"] = new LiquidType
		{
			localeName = "ceftriaxone",
			color = new Color(0.275f, 0.812f, 0.188f),
			valuePerLiter = 170f,
			onDrink = delegate
			{
			},
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				limb.body.antibioticImmunityTime += 1125f * num;
				limb.pain += num * 80f;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("disinfectant", 1f)
			}
		},
		["fentanyl"] = new LiquidType
		{
			localeName = "fentanyl",
			color = new Color(0.341f, 0.949f, 1f),
			valuePerLiter = 2000f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.1f;
				body.GetOrAddComponent<Painkillers>().opiateAmount += num * 400f;
			},
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.1f;
				limb.body.GetOrAddComponent<Painkillers>().opiateAmount += num * 420f;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("opiate", 40f)
			}
		},
		["ketchup"] = new LiquidType
		{
			localeName = "ketchup",
			color = new Color32(byte.MaxValue, 43, 43, byte.MaxValue),
			valuePerLiter = 18f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 60f);
				body.Eat(80f * num, 10f * num);
				body.sicknessAmount += 50f * num;
				body.happiness -= 25f * num;
				body.talker.EatMediocre();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("condiment", 1f)
			}
		},
		["milk"] = new LiquidType
		{
			localeName = "milk",
			color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 13f,
			injectionSickness = 0.4f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 90f);
				body.Eat(30f * num, 4f * num);
				body.temperature -= num * 2.5f;
				body.happiness += 5f * num;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.5f)
			}
		},
		["chloroform"] = new LiquidType
		{
			localeName = "chloroform",
			color = new Color32(186, 209, 167, byte.MaxValue),
			valuePerLiter = 12f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.01f;
				CoUtils.instance.DoTimedOp("chloroform", delegate
				{
					body.consciousness = Mathf.MoveTowards(body.consciousness, 0f, 8f);
				}, 180f * num);
			},
			injectable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				CoUtils.instance.DoTimedOp("chloroform", delegate
				{
					limb.body.consciousness = Mathf.MoveTowards(limb.body.consciousness, 0f, 8f);
				}, 180f * num);
			},
			injectionSickness = 0f
		},
		["highgradestimulant"] = new LiquidType
		{
			localeName = "highgradestimulant",
			color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 32f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				CoUtils.instance.DoTimedOp("highgradestimulant", delegate
				{
					HighGradeStimulantStep(body.limbs[0]);
				}, num * 200f);
			},
			injectable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 100f;
				CoUtils.instance.DoTimedOp("highgradestimulant", delegate
				{
					HighGradeStimulantStep(limb);
				}, num * 240f);
			},
			injectionSickness = 0f
		},
		["midgradestimulant"] = new LiquidType
		{
			localeName = "midgradestimulant",
			color = new Color32(209, 209, 209, byte.MaxValue),
			valuePerLiter = 24f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.happiness -= 2f * num;
				body.sicknessAmount += 10f * num;
				body.talker.EatBad();
			},
			injectable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 50f;
				CoUtils.instance.DoTimedOp("midgradestimulant", delegate
				{
					float num2 = CoUtils.instance.DurationOf("midgradestimulant");
					limb.body.stamina += 2f;
					limb.body.consciousness += 2f;
					limb.body.energy += 0.1f;
					limb.body.sicknessAmount += 0.1f;
					limb.body.internalBleeding += 0.06f;
					limb.body.adrenaline += 7f;
					if (Random.value < 0.1f)
					{
						limb.body.miscShakeIntensity += 1.5f;
					}
					if (limb.body.stimulantMultiplier < 0.25f)
					{
						limb.body.stimulantMultiplier += 0.035f;
					}
					if (num2 > 220f)
					{
						if (Random.value < 0.18f)
						{
							limb.body.miscShakeIntensity += 1.5f;
						}
						if (Random.value < 0.1f)
						{
							limb.body.stamina -= 35f;
						}
						if (Random.value < 0.06f)
						{
							limb.body.Ragdoll();
						}
						limb.body.internalBleeding += 0.15f;
						limb.body.brainHealth -= 0.05f;
						if (limb.body.limbs[1].pain < 60f)
						{
							limb.body.limbs[1].pain += 4f;
						}
						limb.body.overdoseIndex = 3;
					}
					if (CoUtils.instance.HighestDurationOf("midgradestimulant") > 59f)
					{
						if (num2 < 30f && Random.value < 0.1f)
						{
							limb.body.stamina -= 25f;
						}
						if (num2 <= 1f)
						{
							limb.body.energy -= 30f;
							limb.body.vomiter.Vomit();
						}
					}
				}, num * 180f);
			},
			injectionSickness = 0f
		},
		["lowgradestimulant"] = new LiquidType
		{
			localeName = "lowgradestimulant",
			color = new Color32(144, 144, 144, byte.MaxValue),
			valuePerLiter = 20f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 40f;
				CoUtils.instance.DoTimedOp("lowgradestimulant", delegate
				{
					LowGradeStimulantStep(body.limbs[1]);
				}, num * 100f);
				body.happiness -= 0.6f * num;
				body.talker.EatMediocre();
			},
			injectable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 40f;
				CoUtils.instance.DoTimedOp("lowgradestimulant", delegate
				{
					LowGradeStimulantStep(limb);
				}, num * 130f);
			},
			injectionSickness = 0f
		},
		["applejuice"] = new LiquidType
		{
			localeName = "applejuice",
			color = new Color32(197, byte.MaxValue, 97, byte.MaxValue),
			valuePerLiter = 14f,
			injectionSickness = 0.4f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 90f);
				body.weightOffset += num;
				body.temperature -= num * 3f;
				body.happiness += 10f * num;
				body.talker.EatGood();
			}
		},
		["orangejuice"] = new LiquidType
		{
			localeName = "orangejuice",
			color = new Color32(byte.MaxValue, 137, 41, byte.MaxValue),
			valuePerLiter = 14f,
			injectionSickness = 0.4f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(num * 9f);
				body.weightOffset += num * 0.07f;
				body.temperature -= num * 0.1f;
				body.happiness -= num * 1.5f;
				body.sicknessAmount -= 2.5f * num;
				body.talker.EatBad();
			}
		},
		["lemonade"] = new LiquidType
		{
			localeName = "lemonade",
			color = new Color32(byte.MaxValue, 247, 97, byte.MaxValue),
			valuePerLiter = 14f,
			injectionSickness = 0.4f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 90f);
				body.weightOffset += num * 2f;
				body.temperature -= num * 3f;
				body.happiness += 12f * num;
				body.talker.EatGood();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.5f)
			}
		},
		["urine"] = new LiquidType
		{
			localeName = "urine",
			color = new Color32(byte.MaxValue, 218, 84, byte.MaxValue),
			valuePerLiter = 2f,
			injectionSickness = 2f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 70f);
				body.temperature -= num * 3f;
				body.happiness -= 25f * num;
				body.sicknessAmount += 75f * num;
				body.talker.EatBad();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.1f)
			}
		},
		["icetea"] = new LiquidType
		{
			localeName = "icedtea",
			color = new Color32(250, 135, 52, byte.MaxValue),
			valuePerLiter = 12f,
			injectionSickness = 0.4f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 90f);
				body.weightOffset += num * 4f;
				body.temperature -= num * 3f;
				body.happiness += 14f * num;
				body.sicknessAmount += 35f * num;
				body.talker.EatGood();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.5f)
			}
		},
		["soup"] = new LiquidType
		{
			localeName = "soup",
			color = new Color32(125, 81, 0, byte.MaxValue),
			valuePerLiter = 20f,
			injectionSickness = 0.6f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 90f);
				body.Eat(100f * num, 5f * num);
				body.happiness += 10f * num;
			}
		},
		["chocolatemilk"] = new LiquidType
		{
			localeName = "chocolatemilk",
			color = new Color32(143, 92, 55, byte.MaxValue),
			valuePerLiter = 10f,
			injectionSickness = 1f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(90f * num);
				body.Eat(25f * num, 3f * num);
				body.happiness += 10f * num;
				body.talker.EatGood();
				body.temperature -= 2.5f * num;
				body.sicknessAmount += 120f * num;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.5f)
			}
		},
		["cereal"] = new LiquidType
		{
			localeName = "cereal",
			color = new Color32(byte.MaxValue, 219, 156, byte.MaxValue),
			valuePerLiter = 20f,
			injectionSickness = 0.9f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 75f);
				body.Eat(60f * num, 4f * num);
				body.temperature -= num * 2.5f;
				body.happiness += 15f * num;
				body.talker.EatGood();
			}
		},
		["coffee"] = new LiquidType
		{
			localeName = "coffee",
			color = new Color32(80, 50, 30, byte.MaxValue),
			valuePerLiter = 14f,
			injectionSickness = 0.7f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(120f * num);
				body.stamina += 250f * num;
				body.energy += 150f * num;
				body.sicknessAmount += 150f * num;
				body.happiness += 25f * num;
				body.weightOffset += num;
				body.talker.EatGood();
				body.caffeinated += 3500f * num;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.3f)
			}
		},
		["energydrink"] = new LiquidType
		{
			localeName = "energydrink",
			color = new Color32(187, 0, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 20f,
			injectionSickness = 0.7f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(90f * num);
				body.stamina += 250f * num;
				body.energy += 200f * num;
				body.sicknessAmount += 200f * num;
				body.happiness += 25f * num;
				body.weightOffset += 4f * num;
				body.talker.EatGood();
				body.caffeinated += 4000f * num;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.3f)
			}
		},
		["sportsdrink"] = new LiquidType
		{
			localeName = "sportsdrink",
			color = new Color32(10, 59, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 20f,
			injectionSickness = 0.4f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(13f * num);
				body.stamina += 25f * num;
				body.energy += 10f * num;
				body.sicknessAmount += 2f * num;
				body.happiness += 0.5f * num;
				body.weightOffset += 1f * num;
				body.talker.EatGood();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.8f)
			}
		},
		["oliveoil"] = new LiquidType
		{
			localeName = "oliveoil",
			color = new Color32(129, 135, 7, 200),
			valuePerLiter = 14f,
			injectionSickness = 1f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(3f * num);
				body.Eat(8f * num, 2f * num);
				body.dirtyness += 5f * num;
				body.sicknessAmount += 12f * num;
				body.happiness -= 1f * num;
				body.talker.EatMediocre();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("fat", 0.25f)
			}
		},
		["hotsauce"] = new LiquidType
		{
			localeName = "hotsauce",
			color = new Color32(byte.MaxValue, 0, 0, 200),
			valuePerLiter = 14f,
			injectionSickness = 1f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(4f * num);
				body.Eat(5f * num, 2f * num);
				body.sicknessAmount += 3f * num;
				body.limbs[0].pain += 12f * num;
				CoUtils.instance.DoTimedOp("hotsauce", delegate
				{
					if (body.temperature < 41f)
					{
						body.temperature += 0.2f;
					}
				}, 25f);
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("condiment", 1f)
			}
		},
		["icecream"] = new LiquidType
		{
			localeName = "icecream",
			color = new Color32(237, byte.MaxValue, 189, byte.MaxValue),
			valuePerLiter = 26f,
			injectionSickness = 1f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(6f * num);
				body.Eat(6f * num, 1.2f * num);
				body.sicknessAmount += 1.5f * num;
				body.happiness += 1.5f * num;
				CoUtils.instance.DoTimedOp("icecream", delegate
				{
					if (body.temperature > 28.5f)
					{
						body.temperature -= 0.2f;
					}
				}, 15f);
				body.talker.EatGood();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.5f)
			}
		},
		["yogurt"] = new LiquidType
		{
			localeName = "yogurt",
			color = new Color32(213, 235, 240, byte.MaxValue),
			valuePerLiter = 26f,
			injectionSickness = 1f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(4f * num);
				body.Eat(9f * num, 0.4f * num);
				body.sicknessAmount += 1f * num;
				body.happiness += 0.2f * num;
				body.temperature -= 0.15f * num;
			}
		},
		["mold"] = new LiquidType
		{
			localeName = "mold",
			color = new Color32(63, 79, 50, byte.MaxValue),
			valuePerLiter = 0f,
			injectionSickness = 3f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Eat(3f * num, 0.4f * num);
				body.sicknessAmount += 30f * num;
				body.happiness -= 4f * num;
				if (ml > 25f)
				{
					body.vomiter.Vomit();
				}
			}
		},
		["powderedmilk"] = new LiquidType
		{
			localeName = "powderedmilk",
			color = new Color32(242, 242, 242, byte.MaxValue),
			valuePerLiter = 26f,
			injectionSickness = 1f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 30f;
				body.thirst -= 8f * num;
				body.Eat(8f * num, 0.4f * num);
				body.sicknessAmount += 4f * num;
				body.happiness -= 0.5f * num;
				body.talker.EatMediocre();
			}
		},
		["radwater"] = new LiquidType
		{
			localeName = "radwater",
			color = new Color32(121, 224, 221, byte.MaxValue),
			valuePerLiter = 10f,
			injectionSickness = 0.2f,
			onDrink = delegate(float ml, Body body)
			{
				float hundredMl = ml / 100f;
				body.Drink(9f * hundredMl);
				body.temperature -= 0.2f * hundredMl;
				CoUtils.instance.DoTimedOp("radwater", delegate
				{
					body.radiationSickness += 0.35f * hundredMl;
				}, 45f);
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.8f)
			}
		},
		["mercury"] = new LiquidType
		{
			localeName = "mercury",
			color = new Color32(77, 77, 77, byte.MaxValue),
			valuePerLiter = 8f,
			injectionSickness = 10f,
			onDrink = delegate(float ml, Body body)
			{
				float hundredMl = ml / 100f;
				body.sicknessAmount += hundredMl * 25f;
				body.bloodViscosity -= hundredMl * 20f;
				body.happiness -= hundredMl * 4f;
				CoUtils.instance.DoTimedOp("mercury", delegate
				{
					body.brainHealth -= 0.05f * hundredMl;
				}, 200f);
				body.talker.EatBad();
			}
		},
		["soda"] = new LiquidType
		{
			localeName = "soda",
			color = new Color32(112, 94, 73, byte.MaxValue),
			valuePerLiter = 16f,
			injectionSickness = 0.6f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(90f * num);
				body.temperature -= num * 3f;
				body.stamina += 40f * num;
				body.energy += 40f * num;
				body.sicknessAmount += 50f * num;
				body.happiness += 15f * num;
				body.weightOffset += 4f * num;
				body.talker.EatGood();
				body.caffeinated += 800f * num;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.5f)
			}
		},
		["alcohol"] = new LiquidType
		{
			localeName = "alcohol",
			color = new Color32(118, 230, 103, byte.MaxValue),
			valuePerLiter = 12f,
			injectionSickness = 0.75f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 75f);
				body.temperature -= num * 2.5f;
				body.sicknessAmount += 70f * num;
			},
			healthUsable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				limb.pain += 10f * num;
				limb.SetDisinfect(350f * Mathf.Clamp01(num));
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("disinfectant", 0.4f),
				new CraftingQuality("water", 0.7f)
			}
		},
		["bleach"] = new LiquidType
		{
			localeName = "bleach",
			color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 6f,
			injectionSickness = 5f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.01f;
				body.Drink(num * 7f);
				body.talker.EatBad();
				body.happiness -= num * 5f;
				CoUtils.instance.DoTimedOp("bleach", delegate
				{
					body.thirst -= 0.2f;
					body.limbs[0].skinHealth -= 0.1f;
					body.limbs[1].muscleHealth -= 0.225f;
					if (body.limbs[1].pain < 50f)
					{
						body.limbs[1].pain += 1.5f;
					}
					body.sicknessAmount += 0.3f;
				}, 300f * num);
			},
			healthUsable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.01f;
				limb.pain += 25f * num;
				limb.muscleHealth -= 15f * num;
				limb.skinHealAmount -= 20f * num;
				limb.infectionAmount -= 5f * num;
				limb.SetDisinfect(400f * Mathf.Clamp01(num));
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("disinfectant", 0.8f)
			}
		},
		["reliefcream"] = new LiquidType
		{
			localeName = "reliefcream",
			color = new Color32(189, 91, 201, byte.MaxValue),
			valuePerLiter = 120f,
			injectionSickness = 0.75f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.sicknessAmount += num * 100f;
				if (Random.value < ml / 80f)
				{
					body.vomiter.Vomit();
				}
			},
			healthUsable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.1f;
				limb.skinHealAmount += num * 3f;
				CoUtils.instance.DoTimedOp("reliefcream" + limb.name, delegate
				{
					limb.pain = Mathf.Lerp(limb.pain, limb.pain * 0.1f, 0.15f);
				}, 15f * num);
				limb.SetDisinfect(300f * Mathf.Clamp01(num));
				Sound.Play("cream", limb.body.transform.position);
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("disinfectant", 1f)
			}
		},
		["woundglue"] = new LiquidType
		{
			localeName = "woundglue",
			color = new Color32(201, 201, 201, byte.MaxValue),
			valuePerLiter = 150f,
			injectionSickness = 0.75f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.sicknessAmount += num * 100f;
				if (Random.value < ml / 80f)
				{
					body.vomiter.Vomit();
				}
				body.talker.EatBad();
			},
			healthUsable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 20f;
				Sound.Play("cream", limb.body.transform.position);
				limb.skinHealAmount += num * 10f;
				limb.muscleHealth += num * 5f;
				limb.bandageSlowAmount += num * 30f;
				limb.infectionAmount -= num * 5f;
				limb.SetDisinfect(300f * Mathf.Clamp01(num));
				limb.pain *= 0.9f * Mathf.Clamp01(num);
				limb.body.bloodViscosity += 15f * Mathf.Clamp01(num);
				limb.body.sicknessAmount += 2.5f * num;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("disinfectant", 0.8f)
			}
		},
		["braingrow"] = new LiquidType
		{
			localeName = "braingrow",
			color = new Color32(192, 83, 94, byte.MaxValue),
			valuePerLiter = 500f,
			onDrink = delegate(float ml, Body body)
			{
				float twentyML = ml * 0.05f;
				CoUtils.instance.DoTimedOp("braingrow", delegate
				{
					if (body.alive)
					{
						body.brainHealth += 0.1f * twentyML;
						body.strokeAmount -= 1.5f;
					}
				}, 100f);
				body.happiness -= twentyML * 5f;
				body.sicknessAmount += twentyML * 20f;
				body.talker.EatBad();
				Sound.Play("pills", body.transform.position);
				if (body.brainGrowSickness > 0f || ml > 40f)
				{
					body.shock = twentyML * 10f;
					body.Ragdoll();
					if (!body.GetComponent<MindwipeScript>())
					{
						body.gameObject.AddComponent<MindwipeScript>();
					}
				}
				body.brainGrowSickness = twentyML * 60f * 20f;
				if (Random.Range(0f, 1f) < twentyML * 0.5f)
				{
					body.vomiter.Vomit();
				}
			}
		},
		["mindwipe"] = new LiquidType
		{
			localeName = "mindwipe",
			color = new Color32(33, 72, 94, byte.MaxValue),
			valuePerLiter = 1333f,
			onDrink = delegate(float ml, Body body)
			{
				_ = ml / 30f;
				if (!body.GetComponent<MindwipeScript>())
				{
					body.AddComponent<MindwipeScript>();
				}
			}
		},
		["antidepressants"] = new LiquidType
		{
			localeName = "antidepressants",
			color = new Color32(100, 161, 133, 146),
			valuePerLiter = 120f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.05f;
				body.GetOrAddComponent<Antidepressants>().TakeDose(num * 100f);
				body.happiness += 1f * num;
				Sound.Play("pills", body.transform.position);
			}
		},
		["antibiotics"] = new LiquidType
		{
			localeName = "antibiotics",
			color = new Color32(158, 93, 236, byte.MaxValue),
			valuePerLiter = 180f,
			injectionSickness = 0.5f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.1f;
				body.happiness -= 0.5f * num;
				body.septicShock -= 2.5f * num;
				body.antibioticImmunityTime += 250f * num;
				Sound.Play("pills", body.transform.position);
			}
		},
		["antivenom"] = new LiquidType
		{
			localeName = "antivenom",
			color = new Color32(77, byte.MaxValue, 112, byte.MaxValue),
			valuePerLiter = 130f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.02f;
				limb.body.venomTotal -= num * 40f;
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.02f;
				body.sicknessAmount += 12f * num;
			}
		},
		["antiserum"] = new LiquidType
		{
			localeName = "antiserum",
			color = new Color32(117, 62, 93, 201),
			valuePerLiter = 160f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.02f;
				limb.body.septicShock -= 10f * num;
				limb.body.bloodVolume += 3f * num;
				limb.body.antibioticImmunityTime += 300f * num;
				limb.SetDisinfect(180f * Mathf.Clamp01(num));
				Limb[] connectedLimbs = limb.connectedLimbs;
				for (int i = 0; i < connectedLimbs.Length; i++)
				{
					connectedLimbs[i].SetDisinfect(150f * Mathf.Clamp01(num));
				}
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.02f;
				body.antibioticImmunityTime += 50f * num;
				body.sicknessAmount += 15f * num;
			}
		},
		["keratinbooster"] = new LiquidType
		{
			localeName = "keratinbooster",
			color = new Color32(byte.MaxValue, 209, 84, 201),
			valuePerLiter = 150f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml * 0.02f;
				if (limb.body.clawRegrowTime > 3600f)
				{
					limb.body.sicknessAmount += num * 10f;
					limb.body.clawRegrowTime += 1400f * num * 0.1f;
				}
				else
				{
					limb.body.clawRegrowTime += 1400f * num;
				}
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.02f;
				if (body.clawRegrowTime > 3600f)
				{
					body.sicknessAmount += num * 10f;
					body.clawRegrowTime += 1200f * num * 0.1f;
				}
				else
				{
					body.clawRegrowTime += 1200f * num;
				}
			}
		},
		["antirad"] = new LiquidType
		{
			localeName = "antirad",
			color = new Color32(251, 193, 6, byte.MaxValue),
			valuePerLiter = 120f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 20f;
				CoUtils.instance.DoTimedOp("antirad", delegate
				{
					body.radiationSickness -= 0.2f;
					if (CoUtils.instance.DurationOf("antirad") > 180f)
					{
						body.sicknessAmount += 0.6f;
						body.limbs[1].pain += 1.5f;
						body.overdoseIndex = 3;
					}
				}, 90f * num);
				Sound.Play("pills", body.transform.position);
			}
		},
		["sleepingpills"] = new LiquidType
		{
			localeName = "sleepingpills",
			color = new Color32(140, 168, 147, byte.MaxValue),
			valuePerLiter = 320f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.2f;
				body.GetOrAddComponent<SleepingPills>().amount += 300f * num;
				Sound.Play("pills", body.transform.position);
			}
		},
		["procoagulant"] = new LiquidType
		{
			localeName = "procoagulant",
			color = new Color32(189, 86, 96, byte.MaxValue),
			valuePerLiter = 250f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 33.34f;
				CoUtils.instance.DoTimedOp("procoagulant", delegate
				{
					limb.body.internalBleeding *= 0.95f;
					limb.body.bloodViscosity += 1.75f;
					limb.body.strokeAmount -= 10f;
					Limb[] limbs = limb.body.limbs;
					for (int i = 0; i < limbs.Length; i++)
					{
						limbs[i].bleedAmount *= 0.96f;
					}
				}, 20f * num);
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 33.34f;
				CoUtils.instance.DoTimedOp("procoagulant", delegate
				{
					body.internalBleeding *= 0.95f;
					body.bloodViscosity += 1.75f;
					body.strokeAmount -= 10f;
					Limb[] limbs = body.limbs;
					for (int i = 0; i < limbs.Length; i++)
					{
						limbs[i].bleedAmount *= 0.96f;
					}
				}, 12f * num);
				body.happiness -= 2f * num;
			}
		},
		["epinephrine"] = new LiquidType
		{
			localeName = "epinephrine",
			color = new Color32(161, byte.MaxValue, 233, byte.MaxValue),
			valuePerLiter = 80f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 20f;
				CoUtils.instance.DoTimedOp("epinephrine", delegate
				{
					limb.body.adrenaline = 100f;
					if (limb.body.alive && limb.body.inCardiacArrest && Random.value < 0.05f)
					{
						limb.body.heartRate = 200f;
						limb.body.fibrillationProgress = 50f;
					}
					if (CoUtils.instance.DurationOf("epinephrine") > 240f)
					{
						limb.body.TryStartFibrillation(forced: true);
					}
				}, 120f * num);
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 20f;
				CoUtils.instance.DoTimedOp("epinephrine", delegate
				{
					body.adrenaline = 100f;
					if (body.alive && body.inCardiacArrest && Random.value < 0.05f)
					{
						body.heartRate = 200f;
						body.fibrillationProgress = 50f;
					}
					if (CoUtils.instance.DurationOf("epinephrine") > 240f)
					{
						body.TryStartFibrillation(forced: true);
					}
				}, 20f * num);
			}
		},
		["oxyline"] = new LiquidType
		{
			localeName = "oxyline",
			color = new Color32(77, byte.MaxValue, 222, byte.MaxValue),
			valuePerLiter = 80f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 30f;
				CoUtils.instance.DoTimedOp("oxyline", delegate
				{
					limb.body.respiratoryRate += 2.5f;
					limb.body.bloodOxygen += 1.666f;
					limb.body.stamina += 2.5f;
					limb.body.fibrillationProgress -= 1.2f;
					limb.body.bloodVolume += 0.1f;
				}, 60f * num);
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 30f;
				CoUtils.instance.DoTimedOp("oxylinedrink", delegate
				{
					body.limbs[1].pain += 8f;
					body.limbs[1].muscleHealth -= 2.5f;
					body.shock += 28f;
					if (!body.standing)
					{
						Limb[] limbs = body.limbs;
						for (int i = 0; i < limbs.Length; i++)
						{
							limbs[i].rb.velocity += Random.insideUnitCircle * 3.5f;
						}
					}
				}, 10f * num);
			}
		},
		["sodiumnitroprusside"] = new LiquidType
		{
			localeName = "sodiumnitroprusside",
			color = new Color32(207, 88, 41, byte.MaxValue),
			valuePerLiter = 150f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 20f;
				limb.body.bloodPressureChangeFromMedicine += num * 120f;
			},
			onDrink = delegate(float ml, Body body)
			{
				body.sicknessAmount += ml * 0.05f;
			}
		},
		["vasopressin"] = new LiquidType
		{
			localeName = "vasopressin",
			color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 160),
			valuePerLiter = 150f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 20f;
				limb.body.bloodPressureChangeFromMedicine -= num * 120f;
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 20f;
				body.bloodPressureChangeFromMedicine -= num * 10f;
			}
		},
		["amiodarone"] = new LiquidType
		{
			localeName = "amiodarone",
			color = new Color32(byte.MaxValue, 220, 212, 160),
			valuePerLiter = 185f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 20f;
				CoUtils.instance.DoTimedOp("amiodarone", delegate
				{
					if (limb.body.fibrillationProgress > 0f)
					{
						limb.body.fibrillationProgress = Mathf.MoveTowards(limb.body.fibrillationProgress, 0f, 2f);
					}
					limb.body.limbs[0].muscleHealth -= 0.25f;
					limb.body.limbs[1].muscleHealth -= 0.25f;
					limb.body.limbs[2].muscleHealth -= 0.25f;
				}, 60f * num);
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 20f;
				body.sicknessAmount += num * 2f;
			}
		},
		["streptokinase"] = new LiquidType
		{
			localeName = "streptokinase",
			color = new Color32(66, 126, 130, byte.MaxValue),
			valuePerLiter = 150f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 33.334f;
				limb.body.bloodViscosity -= 50f * num;
				limb.body.sicknessAmount += 5f * num;
				Sound.Play("syringe", limb.body.transform.position);
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 33.334f;
				body.bloodViscosity -= 25f * num;
				body.sicknessAmount += 10f * num;
				Sound.Play("drink", body.transform.position);
			}
		},
		["saline"] = new LiquidType
		{
			localeName = "saline",
			color = new Color32(196, 196, 196, byte.MaxValue),
			valuePerLiter = 20f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 750f;
				limb.body.bloodVolume += 40f * num;
				limb.body.bloodViscosity -= 50f * num;
				limb.body.thirst += 70f * num;
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 750f;
				body.thirst += 70f * num;
				body.sicknessAmount += 32f * num;
				body.happiness -= 4f * num;
				body.talker.EatMediocre();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 1f)
			}
		},
		["ringersolution"] = new LiquidType
		{
			localeName = "ringersolution",
			color = new Color(0.929f, 0.929f, 0.929f),
			valuePerLiter = 24f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 700f;
				limb.body.bloodVolume += 35f * num;
				limb.body.bloodViscosity -= 40f * num;
				limb.body.thirst += 60f * num;
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 700f;
				body.thirst += 60f * num;
				body.sicknessAmount += 24f * num;
				body.happiness -= 4f * num;
				body.talker.EatMediocre();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.8f)
			}
		},
		["blood"] = new LiquidType
		{
			localeName = "blood",
			color = new Color32(byte.MaxValue, 201, 0, byte.MaxValue),
			valuePerLiter = 20f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 750f;
				limb.body.bloodVolume += 30f * num;
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 750f;
				body.thirst += 20f * num;
				body.sicknessAmount += 60f * num;
				body.happiness -= 7f * num;
				body.talker.EatBad();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("blood", 1f)
			}
		},
		["redblood"] = new LiquidType
		{
			localeName = "redblood",
			color = new Color32(199, 10, 10, byte.MaxValue),
			valuePerLiter = 18f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 750f;
				limb.body.bloodVolume += 30f * num;
				limb.body.sicknessAmount += 50f * num;
				limb.body.septicShock += 40f * num;
				limb.muscleHealth -= 30f * num;
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 750f;
				body.thirst += 20f * num;
				body.sicknessAmount += 70f * num;
				body.happiness -= 7f * num;
				body.talker.EatBad();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("blood", 1.2f)
			}
		},
		["alienblood"] = new LiquidType
		{
			localeName = "alienblood",
			color = new Color32(byte.MaxValue, 235, 18, byte.MaxValue),
			valuePerLiter = 20f,
			injectable = true,
			injectionSickness = 0f,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 750f;
				limb.body.bloodVolume += 26f * num;
				limb.body.septicShock += 10f * num;
				limb.body.sicknessAmount += 20f * num;
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 750f;
				body.thirst += 20f * num;
				body.sicknessAmount += 60f * num;
				body.happiness -= 7f * num;
				body.talker.EatBad();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("blood", 0.8f)
			}
		},
		["disinfectant"] = new LiquidType
		{
			localeName = "disinfectant",
			color = new Color32(200, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			valuePerLiter = 55f,
			healthUsable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 10f;
				limb.pain += 10f * Mathf.Clamp01(num);
				limb.SetDisinfect(Mathf.Min(240f * num, 240f));
			},
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.thirst += 4f * num;
				body.sicknessAmount += 15f * num;
				body.limbs[0].SetDisinfect(Mathf.Clamp01(num) * 240f);
				body.talker.EatBad();
			},
			injectionSickness = 1.25f,
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("disinfectant", 1.5f)
			}
		},
		["groundwater"] = new LiquidType
		{
			localeName = "groundwater",
			color = new Color32(89, 138, 212, byte.MaxValue),
			valuePerLiter = 4f,
			onDrink = delegate(float ml, Body body)
			{
				bool flag = body.HoldingItem(body.handSlot) && body.GetItem(body.handSlot).id == "filterstraw";
				float num = ml * 0.001f;
				body.Drink(num * 80f);
				body.temperature -= num * 2.5f;
				if (Random.value > 0.5f && !flag)
				{
					body.sicknessAmount += Random.Range(7f, 15f);
				}
				if (!flag)
				{
					body.talker.EatMediocre();
					body.happiness -= 0.5f;
				}
			},
			injectionSickness = 0.15f,
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.4f)
			}
		},
		["lumalgae"] = new LiquidType
		{
			localeName = "algae",
			color = new Color32(33, 153, 0, byte.MaxValue),
			valuePerLiter = 2f,
			injectionSickness = 1.6f,
			onDrink = delegate(float ml, Body body)
			{
				bool flag = body.HoldingItem(body.handSlot) && body.GetItem(body.handSlot).id == "filterstraw";
				float num = ml * 0.001f;
				body.Drink(num * 65f);
				body.Eat(num * 20f, num);
				body.temperature -= num * 2.5f;
				body.talker.EatBad();
				body.happiness -= 1f;
				if (Random.value > 0.35f)
				{
					body.sicknessAmount += Random.Range(num * 60f, num * 85f) * (flag ? 0.3f : 1f);
				}
				if (Random.value < 0.1f && !flag)
				{
					body.vomiter.Vomit();
				}
			}
		},
		["oil"] = new LiquidType
		{
			localeName = "oil",
			color = new Color32(71, 50, 21, byte.MaxValue),
			valuePerLiter = 1f,
			injectionSickness = 5f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 10f);
				body.Eat(num * 20f, 30f * num);
				body.talker.EatBad();
				body.happiness -= 3f;
				body.sicknessAmount += num * 300f;
			}
		},
		["sap"] = new LiquidType
		{
			localeName = "sap",
			color = new Color32(247, 189, 52, byte.MaxValue),
			valuePerLiter = 15f,
			injectionSickness = 2f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(40f * num);
				body.Eat(45f * num, 5f * num);
				body.talker.EatGood();
				body.happiness += 0.5f;
				body.sicknessAmount += Random.Range(8f, 9f) * num;
			}
		},
		["dirtywater"] = new LiquidType
		{
			localeName = "dirtywater",
			color = new Color32(153, 126, 67, byte.MaxValue),
			valuePerLiter = 0f,
			injectionSickness = 2f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				bool num2 = body.HoldingItem(body.handSlot) && body.GetItem(body.handSlot).id == "filterstraw";
				body.Drink(60f * num);
				body.temperature -= num * 2.5f;
				if (!num2)
				{
					body.sicknessAmount += Random.Range(60f, 80f) * num;
					body.talker.EatMediocre();
					body.happiness -= 1f;
				}
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("water", 0.2f)
			}
		},
		["fat"] = new LiquidType
		{
			localeName = "fat",
			color = new Color32(209, 190, 63, byte.MaxValue),
			valuePerLiter = 14f,
			injectionSickness = 2f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(num * 4f);
				body.Eat(num * 5f, num * 0.8f);
				body.happiness -= num * 0.5f;
				body.sicknessAmount += num * 4f;
				body.talker.EatMediocre();
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("fat", 1f)
			}
		},
		["soap"] = new LiquidType
		{
			localeName = "soap",
			color = new Color32(161, byte.MaxValue, 186, byte.MaxValue),
			valuePerLiter = 14f,
			injectionSickness = 1.6f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml / 100f;
				body.Drink(num * 4f);
				body.sicknessAmount += num * 20f;
				body.happiness -= 2f * num;
				body.talker.EatBad();
			},
			healthUsable = true,
			onHealthUse = delegate(float ml, Limb limb)
			{
				float num = ml / 100f;
				limb.SetDisinfect(Mathf.Clamp01(num) * 30f);
				limb.body.dirtyness -= num * 25f;
			},
			qualities = new List<CraftingQuality>
			{
				new CraftingQuality("disinfectant", 0.05f)
			}
		},
		["producejuice"] = new LiquidType
		{
			localeName = "producejuice",
			color = new Color32(byte.MaxValue, 254, 181, byte.MaxValue),
			valuePerLiter = 14f,
			injectionSickness = 0.9f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 85f);
				body.temperature -= num * 3f;
				body.happiness += 0.25f * num;
				body.Eat(num * 20f, num * 2f);
			}
		},
		["refinedjuice"] = new LiquidType
		{
			localeName = "refinedjuice",
			color = new Color32(byte.MaxValue, 225, 115, byte.MaxValue),
			valuePerLiter = 18f,
			injectionSickness = 0.7f,
			onDrink = delegate(float ml, Body body)
			{
				float num = ml * 0.001f;
				body.Drink(num * 95f);
				body.temperature -= num * 2f;
				body.happiness += 2f * num;
				body.Eat(num * 25f, num * 2f);
				body.talker.EatGood();
			}
		}
	};

	private static void HighGradeStimulantStep(Limb limb)
	{
		float num = CoUtils.instance.DurationOf("highgradestimulant");
		limb.body.stamina += 3f;
		limb.body.consciousness += 3.5f;
		limb.body.adrenaline += 25f;
		if (Random.value < 0.1f)
		{
			limb.body.miscShakeIntensity += 3f;
		}
		if (limb.body.stimulantMultiplier < 0.32f)
		{
			limb.body.stimulantMultiplier += 0.05f;
		}
		if (num > 320f)
		{
			if (Random.value < 0.18f)
			{
				limb.body.miscShakeIntensity += 3f;
			}
			if (Random.value < 0.06f)
			{
				limb.body.Ragdoll();
			}
			if (Random.value < 0.05f)
			{
				limb.body.consciousness -= 50f;
			}
			if (Random.value < 0.05f)
			{
				limb.body.temperature -= 1f;
			}
			if (Random.value < 0.03f)
			{
				limb.body.StartCoroutine("BrainControlReverse");
			}
			if (limb.body.stimulantMultiplier > -0.5f)
			{
				limb.body.stimulantMultiplier -= 0.1f;
			}
			limb.body.overdoseIndex = 3;
		}
		if (CoUtils.instance.HighestDurationOf("highgradestimulant") > 80f && num <= 1f)
		{
			limb.body.energy -= 10f;
			if (CoUtils.instance.HighestDurationOf("highgradestimulant") > 320f)
			{
				limb.body.energy -= 30f;
				limb.body.vomiter.Vomit();
			}
		}
	}

	private static void LowGradeStimulantStep(Limb limb)
	{
		float num = CoUtils.instance.DurationOf("lowgradestimulant");
		limb.body.stamina += 1.5f;
		limb.body.consciousness += 1f;
		limb.body.energy += 0.025f;
		limb.body.sicknessAmount += 0.18f;
		limb.body.adrenaline += 20f;
		limb.body.temperature += 0.03f;
		if (Random.value < 0.2f)
		{
			limb.body.miscShakeIntensity += 1.5f;
		}
		if (limb.body.stimulantMultiplier < 0.175f)
		{
			limb.body.stimulantMultiplier += 0.035f;
		}
		if (num > 160f)
		{
			if (Random.value < 0.2f)
			{
				limb.body.miscShakeIntensity += 1.5f;
			}
			if (Random.value < 0.1f)
			{
				limb.body.stamina -= 25f;
			}
			if (Random.value < 0.04f)
			{
				limb.body.energy -= 10f;
			}
			if (Random.value < 0.075f)
			{
				limb.body.bloodOxygen -= 3f;
			}
			if (Random.value < 0.1f)
			{
				limb.body.Ragdoll();
			}
			if (Random.value < 0.06f)
			{
				limb.body.consciousness = 0f;
			}
			if (Random.value < 0.035f)
			{
				limb.body.vomiter.Vomit();
			}
			if (Random.value < 0.02f)
			{
				limb.body.adrenaline = 0f;
			}
			if (Random.value < 0.03f)
			{
				limb.body.StartCoroutine("BrainControlReverse");
			}
			limb.body.temperature += 0.04f;
			limb.body.brainHealth -= 0.08f;
			if (limb.body.limbs[1].pain < 60f)
			{
				limb.body.limbs[1].pain += 4f;
			}
			if (limb.body.limbs[0].pain < 60f)
			{
				limb.body.limbs[0].pain += 4f;
			}
			limb.body.overdoseIndex = 3;
		}
		if (CoUtils.instance.HighestDurationOf("lowgradestimulant") > 50f)
		{
			if (num < 25f && limb.body.consciousness > num * 4f)
			{
				limb.body.consciousness = num * 4f;
			}
			if (num <= 1f)
			{
				limb.body.energy -= 50f;
				limb.body.consciousness = 0f;
				limb.body.vomiter.Vomit();
			}
		}
	}

	public static bool LiquidExists(string id)
	{
		return Registry.ContainsKey(id);
	}
}

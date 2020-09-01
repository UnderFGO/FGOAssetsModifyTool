﻿using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace FGOAssetsModifyTool
{
	class Program
	{
		static string path = Directory.GetCurrentDirectory();
		static DirectoryInfo folder = new DirectoryInfo(path + @"\Android\");
		static DirectoryInfo scriptsFolder = new DirectoryInfo(path + @"\Android\Scripts");
		static DirectoryInfo gamedata = new DirectoryInfo(path + @"\Android\gamedata\");
		static DirectoryInfo decrypt = new DirectoryInfo(path + @"\Decrypt\");
		static DirectoryInfo encrypt = new DirectoryInfo(path + @"\Encrypt\");
		static DirectoryInfo decryptScripts = new DirectoryInfo(path + @"\DecryptScripts\");
		static DirectoryInfo encryptScripts = new DirectoryInfo(path + @"\EncryptScripts\");

		static string AssetStorageFilePath = folder.FullName + "AssetStorage_dec.txt";
		static DirectoryInfo AssetsFolder = new DirectoryInfo(path + @"\assets\bin\Data\");

		static string GetAssetStorage(string assetBundleKey)
        {
			string assetStorage = HttpRequest
				.Get($"https://cdn.data.fate-go.jp/AssetStorages/{assetBundleKey}Android/AssetStorage.txt")
				.ToText();
			return CatAndMouseGame.MouseGame8(assetStorage);
		}

		static void DecryptAssetList()
        {
			string[] assetStore = File.ReadAllLines(AssetStorageFilePath);
			Console.WriteLine("Parsing json...");
			JArray AudioArray = new JArray();
			//JArray MovieArray = new JArray();
			JArray AssetArray = new JArray();
			for (int i = 2; i < assetStore.Length; ++i)
			{
				string[] tmp = assetStore[i].Split(',');
				string assetName;
				string fileName;

				//if (tmp[1] == "SYSTEM")
    //            {
				//	assetName = tmp[tmp.Length - 1].Replace('/', '_');
				//	fileName = assetName;
				//	AssetArray.Add(new JObject(new JProperty("assetName", assetName), new JProperty("fileName", fileName)));
				//}
				if (tmp[4].Contains("Audio"))
				{
					assetName = tmp[tmp.Length - 1].Replace('/', '@');
					fileName = CatAndMouseGame.GetMD5String(assetName);
					AudioArray.Add(new JObject(new JProperty("audioName", assetName), new JProperty("fileName", fileName)));
				}
				//else if (tmp[4].Contains("Movie"))
				//{
				//    assetName = tmp[tmp.Length - 1].Replace('/', '@');
				//    fileName = CatAndMouseGame.GetMD5String(assetName);
				//    MovieArray.Add(new JObject(new JProperty("movieName", assetName), new JProperty("fileName", fileName)));
				//}
				else if (!tmp[4].Contains("Movie"))
				{
					assetName = tmp[tmp.Length - 1].Replace('/', '@') + ".unity3d";
					fileName = CatAndMouseGame.getShaName(assetName);
					AssetArray.Add(new JObject(new JProperty("assetName", assetName), new JProperty("fileName", fileName)));
				}
			}
			Console.WriteLine("Writing file to: AudioName.json");
			File.WriteAllText(folder.FullName + "AudioName.json", AudioArray.ToString());
			//Console.WriteLine("Writing file to: MovieName.json");
			//File.WriteAllText(folder.FullName + "MovieName.json", MovieArray.ToString());
			Console.WriteLine("Writing file to: AssetName.json");
			File.WriteAllText(folder.FullName + "AssetName.json", AssetArray.ToString());
		}

		static void DownloadAssets()
        {
			
			if (!AssetsFolder.Exists)
            {
				AssetsFolder.Create();
			} else if (!File.Exists(gamedata.FullName + "raw"))
            {
				DownloadTopGameData();
			}
            
            DecryptAssetBundle();

            string assetBundleFolder = JObject.Parse(File.ReadAllText(gamedata.FullName + "assetbundle.json"))["folderName"].ToString();
			string assetStorage = GetAssetStorage(assetBundleFolder);
			File.WriteAllText(AssetStorageFilePath, assetStorage);

			DecryptAssetList();
			var assetList = JArray.Parse(File.ReadAllText(folder.FullName + "AssetName.json"));
			foreach (var asset in assetList)
			{
				string filename = asset["fileName"].ToString();
				string assetName = asset["assetName"].ToString();
				if (!assetName.EndsWith(".unity3d"))
                {
					continue;
                }
				string writePath = AssetsFolder.FullName;
				var names = assetName.Split('@');
				if (names.Length > 1)
                {
					writePath += string.Join(@"\", names);
					string writeDirectory = Path.GetDirectoryName(writePath);
					if (!Directory.Exists(writeDirectory))
                    {
						Directory.CreateDirectory(writeDirectory);
                    }

				} else
                {
					writePath = AssetsFolder.FullName + assetName;
				}
				if (File.Exists(writePath))
                {
					continue;
                }
				try
                {
					byte[] raw = HttpRequest.Get($"https://cdn.data.fate-go.jp/AssetStorages/{assetBundleFolder}Android/{filename}").ToBinary();
					byte[] output = CatAndMouseGame.MouseGame4(raw);
					using (FileStream fs = new FileStream(writePath, FileMode.OpenOrCreate, FileAccess.Write))
					{
						fs.Write(output, 0, output.Length);
					}
					Console.WriteLine($"{string.Join(@"\", names)} √");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Dwonload Failed: {filename} - {assetName}\n{ex}");
					continue;
				}

			}
		}

		static void DownloadTopGameData()
		{
			JObject res = HttpRequest.Get("https://game.fate-go.jp/gamedata/top?appVer=2.13.2").ToJson();

            if (res["response"][0]["fail"]["action"] != null)
			{
				if (res["response"][0]["fail"]["action"].ToString() == "app_version_up")
				{
					string tmp = res["response"][0]["fail"]["detail"].ToString();
					tmp = Regex.Replace(tmp, @".*新ver.：(.*)、現.*", "$1", RegexOptions.Singleline);
					Console.WriteLine("new version: " + tmp.ToString());
					var result = HttpRequest.Get($"https://game.fate-go.jp/gamedata/top?appVer={tmp}").ToText();
					res = JObject.Parse(result);
					if (!Directory.Exists(gamedata.FullName))
						Directory.CreateDirectory(gamedata.FullName);
					File.WriteAllText(gamedata.FullName + "raw", result);
					File.WriteAllText(gamedata.FullName + "assetbundle", res["response"][0]["success"]["assetbundle"].ToString());
					Console.WriteLine("Writing file to: " + gamedata.FullName + "assetbundle");
					File.WriteAllText(gamedata.FullName + "master", res["response"][0]["success"]["master"].ToString());
					Console.WriteLine("Writing file to: " + gamedata.FullName + "master");
					File.WriteAllText(gamedata.FullName + "webview", res["response"][0]["success"]["webview"].ToString());
					Console.WriteLine("Writing file to: " + gamedata.FullName + "webview");
				}
			}
		}

		static void DecryptAssetBundle()
		{
			string data = File.ReadAllText(gamedata.FullName + "assetbundle");
			Dictionary<string, object> dictionary = (Dictionary<string, object>)MasterDataUnpacker.MouseInfoMsgPack(Convert.FromBase64String(data));
			File.WriteAllText(gamedata.FullName + "assetbundle.json", JsonConvert.SerializeObject(dictionary));
			Console.WriteLine("folder name: " + dictionary["folderName"].ToString());
		}

		static void DisplayMenu()
		{
			Console.Clear();
			try
			{
				Console.WriteLine(
					"1: 加密\t" +
					"2: 解密\n" +
					"3: 加密AssetStorage.txt\t" +
					"4: 解密AssetStorage.txt\t" +
					"5: 把AssetStorage转换为Json格式\n" +
					"6: 加密剧情文本(scripts)\n" +
					"7: 解密剧情文本(scripts)\n" +
					"8: 把国服文本转换为日服适用\n" +
					"9: 从服务器下载游戏数据\n" +
					"0: 导出资源名 - 实际文件名\n" +
					"11: [gamedata/top]解密master(游戏数据)\n" +
					"12: [gamedata/top]解密assetbundle(assets文件夹名)\n" +
					"13: [gamedata/top]解密webview(url)\n" +
					"69: 切换为美服密钥\n" +
					"67: 切换为国服密钥\n" +
					"99: 下载&解密图片资源");
				int arg = Convert.ToInt32(Console.ReadLine());
				
				switch (arg)
				{
					case 69:
						{
							CatAndMouseGame.EN();
							break;
						}
					case 67:
						{
							CatAndMouseGame.CN();
							break;
						}
					case 1:
						{
							foreach (FileInfo file in decrypt.GetFiles("*.bin"))
							{
								Console.WriteLine("Encrypt: " + file.FullName);
								byte[] raw = File.ReadAllBytes(file.FullName);
								byte[] output = CatAndMouseGame.CatGame4(raw);
								if (!Directory.Exists(encrypt.FullName))
									Directory.CreateDirectory(encrypt.FullName);
								File.WriteAllBytes(encrypt.FullName + file.Name, output);
							}
							break;
						}
					case 2:
						{
							foreach (FileInfo file in folder.GetFiles("*.bin"))
							{
								Console.WriteLine("Decrypt: " + file.FullName);
								byte[] raw = File.ReadAllBytes(file.FullName);
								byte[] output = CatAndMouseGame.MouseGame4(raw);
								if (!Directory.Exists(decrypt.FullName))
									Directory.CreateDirectory(decrypt.FullName);
								File.WriteAllBytes(decrypt.FullName + file.Name, output);
							}
							break;
						}
					case 3:
						{
							string data = File.ReadAllText(folder.FullName + "AssetStorage_dec.txt");
							//string tmp = data;
							//tmp = tmp.Trim(new char[]
							//{
							//    '﻿'
							//});
							//int ri = data.IndexOfAny(new char[]
							//{
							//    '\r',
							//    '\n'
							//});
							//if (ri > 1)
							//{
							//    string crcString = tmp.Substring(0, ri);
							//    if (crcString.StartsWith("~"))
							//    {
							//        crcString = crcString.Substring(1);
							//        Console.WriteLine("OldAssetStorageCrc: " + crcString);
							//        tmp = tmp.Substring(ri + 1);
							//        byte[] readData = Encoding.UTF8.GetBytes(tmp);
							//        uint crc = Crc32.Compute(readData);
							//        Console.WriteLine("AssetStorageCrc: " + crc);
							//        data = data.Replace(crcString.ToString(), crc.ToString());
							//    }
							//}
							string loadData = CatAndMouseGame.CatGame8(data);
							File.WriteAllText(folder.FullName + "AssetStorage_enc.txt", loadData);
							Console.WriteLine("Writing file to: " + folder.FullName + "AssetStorage_enc.txt");
							break;
						}
					case 4:
						{
							string data = File.ReadAllText(folder.FullName + "AssetStorage.txt");
							string loadData = CatAndMouseGame.MouseGame8(data);
							File.WriteAllText(folder.FullName + "AssetStorage_dec.txt", loadData);
							Console.WriteLine("Writing file to: " + folder.FullName + "AssetStorage_dec.txt");
							break;
						}
					case 5:
						{
							Console.WriteLine("Reading file from: " + folder.FullName + "AssetStorage_dec.txt");
							string loadData = File.ReadAllText(folder.FullName + "AssetStorage_dec.txt");
							string[] listData = null;
							loadData = loadData.Trim();
							int num2 = loadData.IndexOfAny(new char[] { '\r', '\n' });
							loadData = loadData.Substring(num2 + 1);
							byte[] bytes = Encoding.UTF8.GetBytes(loadData);
							listData = loadData.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
							string[] array = listData[0].Split(',');
							Console.WriteLine("Parsing json...");
							int num4;
							string attrib;
							int size;
							uint crc;
							string name;
							JArray AssetStorageJson = new JArray();
							for (int i = 1; i < listData.Length; i++)
							{
								array = listData[i].Split(',');
								if (array.Length != 5)
								{
									break;
								}
								num4 = int.Parse(array[0].Trim());
								attrib = array[1];
								size = int.Parse(array[2].Trim());
								crc = uint.Parse(array[3].Trim());
								name = array[4];
								AssetStorageJson.Add(new JObject(
									new JProperty("num4", num4.ToString()), new JProperty("attrib", attrib.ToString()),
									new JProperty("size", size.ToString()), new JProperty("crc", crc.ToString()),
									new JProperty("name", name)
									));
							}
							File.WriteAllText(folder.FullName + "AssetStorage.json", AssetStorageJson.ToString());
							Console.WriteLine("Writing file to: " + folder.FullName + "AssetStorage.json");
							break;
						}
					case 6:
						{
							foreach (FileInfo file in decryptScripts.GetFiles("*.txt", SearchOption.AllDirectories))
							{
								Console.WriteLine("Encrypting: " + file.FullName);
								string ScriptsFolderName = Path.GetFileNameWithoutExtension(file.Directory.Name);
								string txt = File.ReadAllText(file.FullName);
								string outputTxt = CatAndMouseGame.CatGame3(txt);
								if (!Directory.Exists(encryptScripts.FullName + ScriptsFolderName))
									Directory.CreateDirectory(encryptScripts.FullName + ScriptsFolderName);
								File.WriteAllText(encryptScripts.FullName + ScriptsFolderName + "\\" + file.Name, outputTxt);
							}
							break;
						}
					case 7:
						{
							foreach (FileInfo file in scriptsFolder.GetFiles("*.txt", SearchOption.AllDirectories))
							{
								Console.WriteLine("Decrypting: " + file.FullName);
								string ScriptsFolderName = Path.GetFileNameWithoutExtension(file.Directory.Name);
								string txt = File.ReadAllText(file.FullName);
								string outputTxt = CatAndMouseGame.MouseGame3(txt);
								if (!Directory.Exists(decryptScripts.FullName + ScriptsFolderName))
									Directory.CreateDirectory(decryptScripts.FullName + ScriptsFolderName);
								File.WriteAllText(decryptScripts.FullName + ScriptsFolderName + "\\" + file.Name, outputTxt);
							}
							break;
						}
					case 8:
						{
							string jptext = File.ReadAllText(decrypt.FullName + "JP.txt");
							jptext = Regex.Replace(jptext, @".*//.*\n", "", RegexOptions.Multiline);
							jptext = Regex.Replace(jptext, "\"$", "\",", RegexOptions.Multiline);
							JObject jp = JObject.Parse(jptext);
							JObject cn = JObject.Parse(File.ReadAllText(decrypt.FullName + "CN.txt"));
							JObject no = new JObject();
							foreach (JProperty jProperty in jp.Properties())
							{
								if (cn[jProperty.Name] != null)
								{
									jp[jProperty.Name] = cn[jProperty.Name];
								}
								else
								{
									no.Add(jProperty.Name, jProperty.Value);
								}
							}
							File.WriteAllText(decrypt.FullName + "LocalizationJpn.txt", jp.ToString());
							File.WriteAllText(decrypt.FullName + "noTranslation.txt", no.ToString());
							break;
						}
					case 9:
						{
							DownloadTopGameData();
							break;
						}
					case 0:
						{
							DecryptAssetList();
							break;
						}
					case 11:
						{
							//游戏数据
							string data = File.ReadAllText(gamedata.FullName + "master");
							if (!Directory.Exists(gamedata.FullName + "unpack_master"))
								Directory.CreateDirectory(gamedata.FullName + "unpack_master");
							Dictionary<string, byte[]> masterData = (Dictionary<string, byte[]>)MasterDataUnpacker.MouseGame2Unpacker(Convert.FromBase64String(data));
							JObject job = new JObject();
							MiniMessagePacker miniMessagePacker = new MiniMessagePacker();
							foreach(KeyValuePair<string, byte[]> item in masterData)
							{
								List<object> unpackeditem = (List<object>)miniMessagePacker.Unpack(item.Value);
								string json = JsonConvert.SerializeObject(unpackeditem, Formatting.Indented);
								File.WriteAllText(gamedata.FullName + "unpack_master/" + item.Key, json);
								Console.WriteLine("Writing file to: " + gamedata.FullName + "unpack_master/" + item.Key);
							}
							break;
						}
					case 12:
						{
							DecryptAssetBundle();
							break;
						}
					case 13:
						{
							string data = File.ReadAllText(gamedata.FullName + "webview");
							Dictionary<string, object> dictionary = (Dictionary<string, object>)MasterDataUnpacker.MouseGame2MsgPack(Convert.FromBase64String(data));
							string str = "baseURL: " + dictionary["baseURL"].ToString() + "\r\ncontactURL: " + dictionary["contactURL"].ToString() + "\r\n";
							Console.WriteLine(str);
							Dictionary<string, object> filePassInfo = (Dictionary<string, object>)dictionary["filePass"];
							foreach (var a in filePassInfo)
							{
								str += a.Key + ": " + a.Value.ToString() + "\r\n";
							}
							File.WriteAllText(gamedata.FullName + "webview.txt", str);
							Console.WriteLine("Writing file to: " + gamedata.FullName + "webview.txt");
							break;
						}
					case 99:
						DownloadAssets();
						break;
					default:
						{
							Console.WriteLine("请输入一个可接受的选项");
							break;
						}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
				Console.ReadKey(true);
			}
		}
		static void Main(string[] args)
		{
			while (true)
			{
				DisplayMenu();
				Console.WriteLine("pause...");
				Console.ReadKey(true);
			}
		}
	}
}
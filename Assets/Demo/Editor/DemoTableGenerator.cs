using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace Demo.Editor
{
    /// <summary>
    /// 원본 프로젝트(ProjectH)의 XML 데이터셋을 읽어,
    /// 비공개 IP 자산을 배제하고 순수한 배틀 룰 수치만 추출하여
    /// 포트폴리오용 데모 XML 데이터셋(Assets/Demo/Resources/XMLData)을 자동 생성하는 에디터 도구입니다.
    /// </summary>
    public static class DemoTableGenerator
    {
        private const string OriginalXmlDir = "Assets/Resources/XMLData";
        private const string DemoXmlDir = "Assets/Demo/Resources/XMLData";

        [MenuItem("Tools/Portfolio/Generate Demo XML Tables (IP Neutral)")]
        public static void GenerateAllDemoTables()
        {
            if (!Directory.Exists(OriginalXmlDir))
            {
                Debug.LogError($"[DemoTableGenerator] Original XML directory not found: {OriginalXmlDir}");
                return;
            }

            if (!Directory.Exists(DemoXmlDir))
            {
                Directory.CreateDirectory(DemoXmlDir);
            }

            GenerateBattleTable();
            GenerateBattleCharacterTable();
            GenerateSkillTable();
            CopyDirectTable("ChainTable.xml");
            CopyDirectTable("BattleHpTable.xml");
            CopyDirectTable("GameSetTable.xml");
            GenerateStringTable();
            GenerateScenarioTextTable();

            AssetDatabase.Refresh();
            Debug.Log("<color=green>[DemoTableGenerator] All Demo XML Tables generated successfully with IP-neutral rules!</color>");
        }

        private static void GenerateBattleTable()
        {
            string srcPath = Path.Combine(OriginalXmlDir, "BattleTable.xml");
            string dstPath = Path.Combine(DemoXmlDir, "BattleTable.xml");

            if (!File.Exists(srcPath)) return;

            XmlDocument srcDoc = new XmlDocument();
            srcDoc.Load(srcPath);

            XmlDocument dstDoc = new XmlDocument();
            XmlElement root = dstDoc.CreateElement("BattleTable");
            dstDoc.AppendChild(root);

            XmlNodeList rows = srcDoc.SelectNodes("//Row | //row");
            if (rows != null)
            {
                foreach (XmlNode row in rows)
                {
                    string id = row.SelectSingleNode("Id")?.InnerText ?? "1001";
                    string limitRound = row.SelectSingleNode("LimitRound")?.InnerText ?? "20";
                    string playerAxis = row.SelectSingleNode("PlayerAxis")?.InnerText ?? "-60, 0";
                    string enemyAxis = row.SelectSingleNode("EnemyAxis")?.InnerText ?? "60, 0";
                    string ponValues = row.SelectSingleNode("PONValues")?.InnerText ?? "0.35, 0.45, 0.65";
                    string eliteValues = row.SelectSingleNode("EliteValues")?.InnerText ?? "0.65, 0.55, 0.35";
                    string bgmName = row.SelectSingleNode("BgmName")?.InnerText ?? "BGM_B_Battle_1";

                    XmlElement newRow = dstDoc.CreateElement("Row");
                    AppendElem(dstDoc, newRow, "Id", id);
                    AppendElem(dstDoc, newRow, "DevNote", $"Demo Battle {id}");
                    AppendElem(dstDoc, newRow, "StoryPlayerCharacter", "101");
                    AppendElem(dstDoc, newRow, "EnemyCharacter", "102");
                    AppendElem(dstDoc, newRow, "PlayerAxis", playerAxis);
                    AppendElem(dstDoc, newRow, "EnemyAxis", enemyAxis);
                    AppendElem(dstDoc, newRow, "BgmName", bgmName);
                    AppendElem(dstDoc, newRow, "BgImgName", "Bg_Demo_Arena");
                    AppendElem(dstDoc, newRow, "BgImgAxis", "0, 0");
                    AppendElem(dstDoc, newRow, "LimitRound", limitRound);
                    AppendElem(dstDoc, newRow, "WinIllust", "-1");
                    AppendElem(dstDoc, newRow, "PONValues", ponValues);
                    AppendElem(dstDoc, newRow, "EliteValues", eliteValues);

                    root.AppendChild(newRow);
                }
            }

            SaveXmlWithDeclaration(dstDoc, dstPath);
            Debug.Log($"[DemoTableGenerator] Generated {dstPath}");
        }

        private static void GenerateBattleCharacterTable()
        {
            string dstPath = Path.Combine(DemoXmlDir, "BattleCharacterTable.xml");
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("BattleCharacterTable");
            doc.AppendChild(root);

            // 1. Player (101)
            XmlElement pRow = doc.CreateElement("Row");
            AppendElem(doc, pRow, "Id", "101");
            AppendElem(doc, pRow, "DevNote", "[Player] Challenger");
            AppendElem(doc, pRow, "Name", "10001");
            AppendElem(doc, pRow, "MaxHp", "50");
            AppendElem(doc, pRow, "CharacterFaceIconName", "CharIcon_Demo_Player");
            AppendElem(doc, pRow, "DefaultSpriteNames", "CharIllust_Demo_Player");
            AppendElem(doc, pRow, "AttackSpriteName", "CharIllust_Demo_Player");
            AppendElem(doc, pRow, "HitSpriteNames", "CharIllust_Demo_Player");
            AppendElem(doc, pRow, "SpecialSkillSpriteName", "CharIllust_Demo_Player");
            AppendElem(doc, pRow, "SetSkillElements", "1001, 1002, 1003");
            AppendElem(doc, pRow, "GetSkills", "2, 2, 2");
            AppendElem(doc, pRow, "SpecialSkillId", "1004");
            AppendElem(doc, pRow, "SpecialSkillGauge", "10");
            AppendElem(doc, pRow, "ChainId", "1001");
            root.AppendChild(pRow);

            // 2. Rival AI (102)
            XmlElement rRow = doc.CreateElement("Row");
            AppendElem(doc, rRow, "Id", "102");
            AppendElem(doc, rRow, "DevNote", "[AI] Rival Duelist");
            AppendElem(doc, rRow, "Name", "10002");
            AppendElem(doc, rRow, "MaxHp", "50");
            AppendElem(doc, rRow, "CharacterFaceIconName", "CharIcon_Demo_Rival");
            AppendElem(doc, rRow, "DefaultSpriteNames", "CharIllust_Demo_Rival");
            AppendElem(doc, rRow, "AttackSpriteName", "CharIllust_Demo_Rival");
            AppendElem(doc, rRow, "HitSpriteNames", "CharIllust_Demo_Rival");
            AppendElem(doc, rRow, "SpecialSkillSpriteName", "CharIllust_Demo_Rival");
            AppendElem(doc, rRow, "SetSkillElements", "2001, 2002, 2003");
            AppendElem(doc, rRow, "GetSkills", "2, 2, 2");
            AppendElem(doc, rRow, "SpecialSkillId", "2004");
            AppendElem(doc, rRow, "SpecialSkillGauge", "10");
            AppendElem(doc, rRow, "ChainId", "1003");
            root.AppendChild(rRow);

            SaveXmlWithDeclaration(doc, dstPath);
            Debug.Log($"[DemoTableGenerator] Generated {dstPath}");
        }

        private static void GenerateSkillTable()
        {
            string dstPath = Path.Combine(DemoXmlDir, "SkillTable.xml");
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("SkillTable");
            doc.AppendChild(root);

            // Player Basic RPS
            AddSkill(doc, root, "1001", "[Player] Rock", "900111", "900112", "Common_Icon_rock", "Rock", "Attack", "2, 1");
            AddSkill(doc, root, "1002", "[Player] Scissors", "900121", "900122", "Common_Icon_scissors", "Scissors", "Attack", "2, 1");
            AddSkill(doc, root, "1003", "[Player] Paper", "900131", "900132", "Common_Icon_paper", "Paper", "Attack", "2, 1");
            AddSkill(doc, root, "1004", "[Player] Emergency Heal", "900211", "900212", "SkillIcon_Demo_Special_Player", "Require", "LoseHeal", "-1");

            // AI Basic RPS
            AddSkill(doc, root, "2001", "[AI] Rock", "900111", "900112", "Common_Icon_rock", "Rock", "Attack", "2, 1");
            AddSkill(doc, root, "2002", "[AI] Scissors", "900121", "900122", "Common_Icon_scissors", "Scissors", "Attack", "2, 1");
            AddSkill(doc, root, "2003", "[AI] Paper", "900131", "900132", "Common_Icon_paper", "Paper", "Attack", "2, 1");
            AddSkill(doc, root, "2004", "[AI] Berserk Strike", "900221", "900222", "SkillIcon_Demo_Special_Rival", "Require", "Berserk", "2, 1, 1");

            // Fool Common
            AddSkill(doc, root, "8001", "[Common] Fool", "900281", "900282", "SkillIcon_Battle_Fool", "Fool", "Attack", "0, 0");

            SaveXmlWithDeclaration(doc, dstPath);
            Debug.Log($"[DemoTableGenerator] Generated {dstPath}");
        }

        private static void AddSkill(XmlDocument doc, XmlElement root, string id, string devNote, string name, string desc, string icon, string element, string action, string actionValues)
        {
            XmlElement row = doc.CreateElement("Row");
            AppendElem(doc, row, "Id", id);
            AppendElem(doc, row, "DevNote", devNote);
            AppendElem(doc, row, "Name", name);
            AppendElem(doc, row, "Desc", desc);
            AppendElem(doc, row, "Icon", icon);
            AppendElem(doc, row, "CastCutScene", "-1");
            AppendElem(doc, row, "SucceseCutScene", "-1");
            AppendElem(doc, row, "Element", element);
            AppendElem(doc, row, "Action", action);
            AppendElem(doc, row, "ActionValues", actionValues);
            root.AppendChild(row);
        }

        private static void AddString(XmlDocument doc, XmlElement root, string id, string kr, string en, string jp)
        {
            XmlElement row = doc.CreateElement("Row");
            AppendElem(doc, row, "Id", id);
            AppendElem(doc, row, "DevNote", $"Text {id}");
            AppendElem(doc, row, "String_KR", kr);
            AppendElem(doc, row, "String_EN", en);
            AppendElem(doc, row, "String_JP", jp);
            root.AppendChild(row);
        }

        private static void GenerateStringTable()
        {
            string dstPath = Path.Combine(DemoXmlDir, "StringTable.xml");
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("StringTable");
            doc.AppendChild(root);

            AddString(doc, root, "10001", "도전자", "Challenger", "チャレンジャー");
            AddString(doc, root, "10002", "라이벌", "Rival", "ライバル");
            AddString(doc, root, "900111", "바위 공격", "Rock Attack", "グー攻撃");
            AddString(doc, root, "900112", "바위 속성 기본 공격입니다.", "Basic Rock attribute attack.", "グー属性の基本攻撃です。");
            AddString(doc, root, "900121", "가위 공격", "Scissors Attack", "チョキ攻撃");
            AddString(doc, root, "900122", "가위 속성 기본 공격입니다.", "Basic Scissors attribute attack.", "チョキ属性の基本攻撃です。");
            AddString(doc, root, "900131", "보 공격", "Paper Attack", "パー攻撃");
            AddString(doc, root, "900132", "보 속성 기본 공격입니다.", "Basic Paper attribute attack.", "パー属性の基本攻撃です。");
            AddString(doc, root, "900211", "응급 회복", "Emergency Heal", "緊急回復");
            AddString(doc, root, "900212", "합에서 패배할 경우 체력을 회복합니다.", "Heals HP when losing a clash.", "合で敗北した時にHPを回復します。");
            AddString(doc, root, "900221", "버서크 스트라이크", "Berserk Strike", "バーサクストライク");
            AddString(doc, root, "900222", "자신의 체력을 소모하여 강력한 피해를 입힙니다.", "Consumes own HP to deal heavy damage.", "自身のHPを消費して強力なダメージを与えます。");
            AddString(doc, root, "900281", "풀", "Fool", "フール");
            AddString(doc, root, "900282", "모든 카드에 패배하지만 합을 무효화합니다.", "Loses to all cards but nullifies clashes.", "全てのカードに敗北しますが合を無効化します。");

            SaveXmlWithDeclaration(doc, dstPath);
            Debug.Log($"[DemoTableGenerator] Generated {dstPath}");
        }

        private static void GenerateScenarioTextTable()
        {
            string dstPath = Path.Combine(DemoXmlDir, "ScenarioTextTable.xml");
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("ScenarioTextTable");
            doc.AppendChild(root);

            XmlElement row = doc.CreateElement("Row");
            AppendElem(doc, row, "Id", "1");
            AppendElem(doc, row, "KrString", "배틀 시작");
            AppendElem(doc, row, "EnString", "Battle Start");
            AppendElem(doc, row, "JpString", "バトル開始");
            root.AppendChild(row);

            SaveXmlWithDeclaration(doc, dstPath);
            Debug.Log($"[DemoTableGenerator] Generated {dstPath}");
        }

        private static void CopyDirectTable(string fileName)
        {
            string srcPath = Path.Combine(OriginalXmlDir, fileName);
            string dstPath = Path.Combine(DemoXmlDir, fileName);

            if (File.Exists(srcPath))
            {
                File.Copy(srcPath, dstPath, true);
                Debug.Log($"[DemoTableGenerator] Synced {fileName} directly from original.");
            }
        }

        private static void AppendElem(XmlDocument doc, XmlElement parent, string tagName, string text)
        {
            XmlElement elem = doc.CreateElement(tagName);
            elem.InnerText = text;
            parent.AppendChild(elem);
        }

        private static void SaveXmlWithDeclaration(XmlDocument doc, string filePath)
        {
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.InsertBefore(dec, doc.DocumentElement);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t",
                Encoding = System.Text.Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                doc.Save(writer);
            }
        }
    }
}

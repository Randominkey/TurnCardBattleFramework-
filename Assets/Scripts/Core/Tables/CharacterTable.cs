using System;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace Core.Table
{
    [Serializable]
    public class CharacterTableRow : TableRow
    {
        [XmlElement("DevNote")] public string devNote;
        [XmlElement("Name")] public int nameStringId;
        [XmlElement("KeyName")] public CharacterKeyName keyName;
        [XmlElement("BodySpriteName")] public string bodySpriteName;
        [XmlElement("FaceName")] public string faceNamesRaw;
        [XmlElement("FaceSpriteName")] public string faceSpriteNamesRaw;
        [XmlElement("FaceSpriteAxis")] public string faceSpriteAxisRaw;
        [XmlElement("CharacterFaceIconName")] public string characterFaceIconName;
        [XmlElement("CharacterHexCode")] public string characterHexCode;

        private CharacterFaceName[] cachedFaceNames;
        public CharacterFaceName[] FaceNames => cachedFaceNames ??= TableUtil.ParseEnumArray<CharacterFaceName>(faceNamesRaw);

        private string[] cachedFaceSpriteNames;
        public string[] FaceSpriteNames => cachedFaceSpriteNames ??= (faceSpriteNamesRaw ?? "").Split(',').Select(s => s.Trim()).ToArray();

        private Vector2[] cachedFaceAxes;
        public Vector2[] FaceAxes => cachedFaceAxes ??= TableUtil.ParseVector2Array(faceSpriteAxisRaw);
    }

    [Serializable]
    [XmlRoot("CharacterTable")]
    public class CharacterTable : TableBase<CharacterTable, CharacterTableRow>
    {
        protected override string fileName => "CharacterTable";
    }
}

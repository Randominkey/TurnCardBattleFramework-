namespace Core.Battle
{
    /// <summary>
    /// 카드에 부착되는 개별 폭탄 효과 데이터.
    /// 작은폭탄/큰폭탄 등 다양한 데미지와 타입을 가질 수 있습니다.
    /// </summary>
    public class BombData
    {
        public float Damage { get; }
        public string BombType { get; }

        public BombData(float damage, string bombType = "Normal")
        {
            Damage = damage;
            BombType = bombType ?? "Normal";
        }
    }
}

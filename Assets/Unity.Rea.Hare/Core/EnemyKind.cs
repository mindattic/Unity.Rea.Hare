namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The spawnable par_enemy objects, numbered as generate_enemy_list's index_* values
    /// (index_hydra = 1 ... index_trilobyte = 9), and obj_scrapmental, level_08's boss, which only its level script
    /// places (generate_enemy_list never picks it).
    /// </summary>
    public enum EnemyKind
    {
        Hydra = 1,
        Sawblade = 2,
        Dagger = 3,
        Mimic = 4,
        Wraith = 5,
        Meatball = 6,
        Phalanx = 7,
        Worm = 8,
        Trilobyte = 9,
        /// <summary>obj_scrapmental (with its obj_shrapnel_piece and obj_shrapnel_harpoon parts).</summary>
        Scrapmental = 10
    }
}

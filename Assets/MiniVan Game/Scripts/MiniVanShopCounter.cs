namespace MiniVanGame
{
    [UnityEngine.DisallowMultipleComponent]
    public sealed class MiniVanShopCounter : UnityEngine.MonoBehaviour, IMiniVanGameModeInteractable
    {
        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || player.GameModeSelectedItem == MiniVanInventoryItem.None)
                return "Select an item to sell";
            int value = MiniVanGameModeEconomy.GetSalePrice(player.GameModeSelectedItem);
            return value > 0
                ? "E - sell " + player.GameModeSelectedItem + " for $" + value
                : "This item cannot be sold";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null) return;
            player.GameModeRequestSellSelected();
        }

        public void PrimaryAction(MiniVanPlayer player) { }

    }
}

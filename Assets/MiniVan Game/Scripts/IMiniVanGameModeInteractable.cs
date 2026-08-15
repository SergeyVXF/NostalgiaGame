namespace MiniVanGame
{
    public interface IMiniVanGameModeInteractable
    {
        string GetPrompt(MiniVanPlayer player);
        void Interact(MiniVanPlayer player);
        void PrimaryAction(MiniVanPlayer player);
    }
}

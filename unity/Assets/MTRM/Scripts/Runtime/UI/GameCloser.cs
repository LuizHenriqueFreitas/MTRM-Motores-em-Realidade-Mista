using UnityEngine;

public class GameCloser : MonoBehaviour
{
    public void QuitGame()
    {
        // Debug.Log é útil para ver no console se o botão funcionou enquanto testa
        Debug.Log("Saindo do jogo...");

        // O comando Application.Quit() encerra a aplicação compilada
        Application.Quit();

        // Se estivermos dentro do Editor da Unity, Application.Quit() não faz nada.
        // O código abaixo garante que o modo Play seja interrompido no Editor.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
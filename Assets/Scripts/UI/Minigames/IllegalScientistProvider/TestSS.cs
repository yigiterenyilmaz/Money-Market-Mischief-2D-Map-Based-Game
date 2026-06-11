using UnityEngine;

// TEMP: Skill node butonundan IllegalScientistProvider UI'ı açmak için test scripti.
// Geri almak için bu dosyayı sil.
public class TEMP_IllegalScientistProviderOpener : MonoBehaviour
{
    private IllegalScientistProviderUI providerUI;

    private void Start()
    {
        providerUI = FindFirstObjectByType<IllegalScientistProviderUI>(FindObjectsInactive.Include);
    }

    // Butona bağla - Inspector'dan OnClick'e ekle
    public void OpenProviderUI()
    {
        if (providerUI == null)
            providerUI = FindFirstObjectByType<IllegalScientistProviderUI>(FindObjectsInactive.Include);

        if (providerUI != null)
        {
            providerUI.OpenAndTriggerOffer();
        }
        else
        {
            Debug.LogError("IllegalScientistProviderUI sahnede bulunamadı!");
        }
    }
}
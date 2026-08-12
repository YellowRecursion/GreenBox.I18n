using System;
using UnityEngine;
using UnityEngine.UI;

public class TextTimerTest : MonoBehaviour
{
    [SerializeField] private Text _text;

    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;

        if (_timer > 11f)
        {
            I18n.SetLocale(I18n.CurrentLocale.Id == "en" ? "ru" : "en");
            _timer = 0f;
        }
        
        _text.text = I18n.Text(3857487432477178769, ("timer", _timer));
    }
}

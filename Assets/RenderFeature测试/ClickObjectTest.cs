using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GolbalSetting
{
    private static GolbalSetting instance;
    
    public Vector3 gobal_pos = Vector3.zero;
    public bool isRendering = false;

    public static GolbalSetting Instance
    {
        get
        {
            if (instance==null)
            {
                instance = new GolbalSetting();
            }

            return instance;
        }
    }
    
}

public class ClickObjectTest : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
{

    private int _defalutLayer;
    private uint _renderLayer;
    
    // Start is called before the first frame update
    void Start()
    {
        _defalutLayer = this.gameObject.layer;
        _renderLayer = GetComponent<Renderer>().renderingLayerMask; 
        Debug.Log("default layer:"+_renderLayer);
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        //this.gameObject.layer = 31;
        GetComponent<Renderer>().renderingLayerMask = ((uint) (1 << 31 - 1));
        Debug.Log("Change object layer:"+GetComponent<Renderer>().renderingLayerMask);
        // GolbalSetting.Instance.isRendering = true;
        // GolbalSetting.Instance.gobal_pos = this.transform.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        this.gameObject.layer = _defalutLayer;
        GetComponent<Renderer>().renderingLayerMask = _renderLayer;
        Debug.Log("Change back object layer:"+_renderLayer);
        //GolbalSetting.Instance.isRendering = false;
        //GolbalSetting.Instance.gobal_pos = this.transform.position;
    }
}

using System.Reflection;
using Android.OS;
using Android.Views;
using Android.Widget;
using Button = Android.Widget.Button;
using Fragment = AndroidX.Fragment.App.Fragment;
using View = Android.Views.View;

namespace LlmInference;

public class SelectionFragment : Fragment,
    View.IOnClickListener
{
    public Action OnModelSelected = null;

    override public View OnCreateView(
        LayoutInflater inflater,
        ViewGroup container,
        Bundle savedInstanceState)
    {
        return inflater.Inflate(Resource.Layout.fragment_selection, container, false);
    }

    override public void OnViewCreated(View view, Bundle savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);
        var container = view.FindViewById<LinearLayout>(Resource.Id.model_list_container);

        var fields = typeof(Model).GetFields(BindingFlags.Static | BindingFlags.Public);
        foreach (var field in fields)
        {
            var itenView = LayoutInflater.FromContext(RequireContext()).
                Inflate(Resource.Layout.item_model, container, false);
            var button = itenView.FindViewById<Button>(Resource.Id.btn_model);          
            button.SetText(field.Name, null);
            button.SetOnClickListener(this);
            
            container.AddView(button);
        }
    }

    public void OnClick(View v)
    {
        var name = (v as Button).Text;
        var fieldInfo = typeof(Model).GetField(name, BindingFlags.Static | BindingFlags.Public);
        var model = fieldInfo.GetValue(null) as Model;
        InferenceModel.Model = model;
        OnModelSelected?.Invoke();
    }
}

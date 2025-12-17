namespace MauiApp3
{
    public partial class MainPage : ContentPage
    {
        Converter converter = new Converter();

        public MainPage()
        {
            InitializeComponent();
            BindingContext = converter;
        }
    }

}

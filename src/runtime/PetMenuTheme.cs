using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
namespace WhiteDragon {
public static class PetMenuTheme {
 public static Brush Color(string hex){return (Brush)new BrushConverter().ConvertFromString(hex);}
 public static TextBlock Text(string text,double size,string color){return new TextBlock{Text=text,FontSize=size,Foreground=Color(color),VerticalAlignment=VerticalAlignment.Center};}
 public static TextBlock Icon(string glyph){return new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=15,Foreground=Color("#8A6CA3"),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};}
 public static void Apply(ContextMenu menu){
  menu.Resources=(ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
   <Style TargetType='{x:Type ContextMenu}'>
    <Setter Property='FontFamily' Value='Microsoft YaHei UI'/><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#3F344B'/><Setter Property='Background' Value='#FFFEFF'/><Setter Property='BorderBrush' Value='#E7DEEE'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='7'/><Setter Property='MinWidth' Value='284'/><Setter Property='HasDropShadow' Value='False'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ContextMenu}'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='16' Padding='{TemplateBinding Padding}'><ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' CanContentScroll='True' MaxHeight='700'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/></ScrollViewer></Border></ControlTemplate></Setter.Value></Setter>
   </Style>
   <Style TargetType='{x:Type MenuItem}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/><Setter Property='Padding' Value='10,8'/><Setter Property='MinHeight' Value='36'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type MenuItem}'>
     <Grid Background='Transparent'><Border x:Name='Row' Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'>
      <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='25'/><ColumnDefinition Width='*'/><ColumnDefinition Width='28'/></Grid.ColumnDefinitions>
       <ContentPresenter x:Name='Icon' ContentSource='Icon' HorizontalAlignment='Left' VerticalAlignment='Center'/>
       <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center' Margin='2,0,7,0'/>
       <TextBlock x:Name='Arrow' Grid.Column='2' Text='&#xE76C;' FontFamily='Segoe MDL2 Assets' FontSize='10' Foreground='#A38AB6' HorizontalAlignment='Right' VerticalAlignment='Center' Visibility='Collapsed'/>
       <Border x:Name='Switch' Grid.Column='2' Width='28' Height='16' Background='#E6E0EB' CornerRadius='8' Visibility='Collapsed'><Ellipse x:Name='Knob' Width='10' Height='10' Fill='White' HorizontalAlignment='Left' Margin='3,0'/></Border>
      </Grid></Border>
      <Popup x:Name='PART_Popup' Placement='Right' PlacementTarget='{Binding RelativeSource={RelativeSource TemplatedParent}}' IsOpen='{Binding IsSubmenuOpen,RelativeSource={RelativeSource TemplatedParent}}' AllowsTransparency='True' Focusable='False' PopupAnimation='None' HorizontalOffset='0' VerticalOffset='-6'>
       <Border Background='#FFFEFF' BorderBrush='#E7DEEE' BorderThickness='1' CornerRadius='12' Padding='6' MinWidth='226'><ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' CanContentScroll='True' MaxHeight='620'><ItemsPresenter x:Name='ItemsPresenter' KeyboardNavigation.DirectionalNavigation='Cycle' KeyboardNavigation.TabNavigation='Cycle' Grid.IsSharedSizeScope='True'/></ScrollViewer></Border>
      </Popup>
     </Grid>
     <ControlTemplate.Triggers>
      <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Row' Property='Background' Value='#F0E9F7'/></Trigger>
      <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Row' Property='Background' Value='#F0E9F7'/></Trigger>
      <Trigger Property='HasItems' Value='True'><Setter TargetName='Arrow' Property='Visibility' Value='Visible'/></Trigger>
      <Trigger Property='IsCheckable' Value='True'><Setter TargetName='Switch' Property='Visibility' Value='Visible'/></Trigger>
      <Trigger Property='IsChecked' Value='True'><Setter TargetName='Switch' Property='Background' Value='#A284BD'/><Setter TargetName='Knob' Property='HorizontalAlignment' Value='Right'/></Trigger>
      <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Row' Property='Opacity' Value='.5'/></Trigger>
     </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
   </Style>
   <Style x:Key='Card' TargetType='{x:Type MenuItem}'><Setter Property='Focusable' Value='False'/><Setter Property='StaysOpenOnClick' Value='True'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type MenuItem}'><ContentPresenter ContentSource='Header'/></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='{x:Type Separator}'><Setter Property='Margin' Value='9,5'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Separator}'><Border Height='1' Background='#EEE8F3'/></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='{x:Type Button}'><Setter Property='Foreground' Value='#725388'/><Setter Property='Background' Value='#F4EFF9'/><Setter Property='Padding' Value='7,9'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='Quick' Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Quick' Property='Background' Value='#E9DFF3'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Quick' Property='Background' Value='#E9DFF3'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Quick' Property='Background' Value='#DECCE9'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  </ResourceDictionary>");
  menu.Style=(Style)menu.Resources[typeof(ContextMenu)];
 }
}
}

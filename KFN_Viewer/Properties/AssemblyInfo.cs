using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// Общие сведения об этой сборке предоставляются следующим набором
// набора атрибутов. Измените значения этих атрибутов, чтобы изменить сведения,
// общие сведения об этой сборке.
[assembly: AssemblyTitle("KFN Viewer")]
[assembly: AssemblyDescription("Viewer for KFN (karafan) files")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("MadLord, 2019")]
[assembly: AssemblyProduct("KFN Viewer")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Установка значения False для параметра ComVisible делает типы в этой сборке невидимыми
// для компонентов COM. Если необходимо обратиться к типу в этой сборке через
// из модели COM задайте для атрибута ComVisible этого типа значение true.
[assembly: ComVisible(false)]

//Чтобы начать создание локализуемых приложений, задайте
//<UICulture>CultureYouAreCodingWith</UICulture> в файле .csproj
//в <PropertyGroup>. Например, при использовании английского (США)
//в исходных файлах, присвойте параметру <UICulture> значение en-US. Затем раскомментируйте
//атрибута NeutralResourceLanguage, расположенного ниже. Обновите "en-US" в
//строке ниже, чтобы сопоставить с параметром UICulture в файле проекта.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //где расположены тематические словари ресурсов
                                     //(используется, если ресурс не найден на странице,
                                     // или в словарях ресурсов приложения)
    ResourceDictionaryLocation.SourceAssembly //где расположен универсальный словарь ресурсов
                                              //(используется, если ресурс не найден на странице,
                                              // приложение или любые тематические словари ресурсов)
)]


// Сведения о версии сборки состоят из указанных ниже четырех значений:
//
//      Основной номер версии
//      Дополнительный номер версии
//   Номер сборки
//      Редакция
//
// Можно задать все значения или принять номер сборки и номер редакции по умолчанию.
// используя "*", как показано ниже:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

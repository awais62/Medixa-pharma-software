using System;
using System.IO;
using System.Reflection;

class Program
{
    static void Main()
    {
        try
        {
            Assembly app = Assembly.LoadFrom(@"PharmaBilling.exe");
            Type vmType = app.GetType("PharmaBilling.Source.ViewModels.PurchaseViewModel");
            object vm = Activator.CreateInstance(vmType);

            // Add Medicine
            Type medType = app.GetType("PharmaBilling.Source.Models.Medicine");
            object med = Activator.CreateInstance(medType);
            medType.GetProperty("MedicineID").SetValue(med, 1);
            medType.GetProperty("Name").SetValue(med, "Test");
            medType.GetProperty("BoxSize").SetValue(med, 1);

            // AddToPurchase
            MethodInfo addToPurch = vmType.GetMethod("AddToPurchase");
            addToPurch.Invoke(vm, new object[] { med, 1 });

            // SavePurchaseWithType
            MethodInfo save = vmType.GetMethod("SavePurchaseWithType");
            object result = save.Invoke(vm, new object[] { "Loose" });
            
            Console.WriteLine("SaveResult: " + result);
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}

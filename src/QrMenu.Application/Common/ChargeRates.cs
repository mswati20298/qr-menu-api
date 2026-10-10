namespace QrMenu.Application.Common;

/// <summary>
/// The GST and service charge rates that amounts were charged at. A bill uses this instead of the restaurant's
/// current settings, which may have changed after the orders were placed (the % would then not match the amounts).
/// Rounded to the nearest 0.5 (rates are whole or half percents).
/// </summary>
public static class ChargeRates
{
    public static (decimal GstPercentage, decimal ServiceChargePercentage) FromAmounts(decimal subtotal, decimal serviceCharge, decimal gst)
    {
        static decimal Half(decimal value) => Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
        var gstBase = subtotal + serviceCharge;
        return (
            gstBase > 0 && gst > 0 ? Half(gst / gstBase * 100) : 0,
            subtotal > 0 && serviceCharge > 0 ? Half(serviceCharge / subtotal * 100) : 0);
    }
}

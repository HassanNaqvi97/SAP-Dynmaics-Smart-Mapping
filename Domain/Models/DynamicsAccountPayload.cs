using System.Text.Json.Serialization;

namespace Domain.Models;

public sealed class DynamicsAccountPayload
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("accountnumber")] public string? AccountNumber { get; set; }
    [JsonPropertyName("telephone1")] public string? Telephone1 { get; set; }
    [JsonPropertyName("fax")] public string? Fax { get; set; }
    [JsonPropertyName("emailaddress1")] public string? EmailAddress1 { get; set; }
    [JsonPropertyName("websiteurl")] public string? WebsiteUrl { get; set; }
    [JsonPropertyName("industrycode")] public string? IndustryCode { get; set; }
    [JsonPropertyName("customertypecode")] public string? CustomerTypeCode { get; set; }
    [JsonPropertyName("statuscode")] public string? StatusCode { get; set; }
    [JsonPropertyName("address1_line1")] public string? Address1Line1 { get; set; }
    [JsonPropertyName("address1_city")] public string? Address1City { get; set; }
    [JsonPropertyName("address1_stateorprovince")] public string? Address1StateOrProvince { get; set; }
    [JsonPropertyName("address1_postalcode")] public string? Address1PostalCode { get; set; }
    [JsonPropertyName("address1_country")] public string? Address1Country { get; set; }
    [JsonPropertyName("vsi_sapbusinesspartnerid")] public string? VsiSapBusinessPartnerId { get; set; }
    [JsonPropertyName("vsi_sapcustomerid")] public string? VsiSapCustomerId { get; set; }
    [JsonPropertyName("vsi_taxid")] public string? VsiTaxId { get; set; }
    [JsonPropertyName("vsi_businesslicensenumber")] public string? VsiBusinessLicenseNumber { get; set; }
    [JsonPropertyName("vsi_customersegment")] public string? VsiCustomerSegment { get; set; }
    [JsonPropertyName("vsi_customertier")] public string? VsiCustomerTier { get; set; }
    [JsonPropertyName("vsi_saleschannel")] public string? VsiSalesChannel { get; set; }
    [JsonPropertyName("creditlimit")] public decimal? CreditLimit { get; set; }
}

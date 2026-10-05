namespace Microsoft.eShopWeb.ApplicationCore.Billing;

/// <summary>
/// The identity of the eShopOnWeb user being enrolled in (or queried for) a
/// subscription. The billing system of record keys its customer records off
/// <see cref="UserId"/>, so repeated enrollments for the same user map to the
/// same external customer.
/// </summary>
public sealed record SubscriberInfo(string UserId, string Email);
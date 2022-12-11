using Automation.BDaq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator
{
    class Program
    {
        private static InstantDoCtrl port;
        private static InstantDiCtrl vstup;
        private static DeviceInformation di;
        //static int smerBehu = 0; // Směr, kterým výtah jede (0 - dolu, 1 - nahoru)
        static bool jedeVytah = false; // Zjištění, zda výtah jede

        static void init()
        {
            port = new InstantDoCtrl();
            vstup = new InstantDiCtrl();
            di = new DeviceInformation();

            di.Description = "PCIE-1730,BID#0";

            port.SelectedDevice = di;
            port.LoadProfile("...\\profil.xml");

            vstup.SelectedDevice = di;
            vstup.LoadProfile("...\\profil.xml");

            port.Write(0, 0xFF);
        }

        static void Main(string[] args)
        {

            init();

            while(true)
            {
                byte operace;
                port.Read(0, out operace);

                byte tlacitka;
                vstup.Read(0, out tlacitka);

                byte cidla;
                vstup.Read(1, out cidla);

                // Zjistovani vsech hodnot a stavu
                
                bool otevrenyDvere = jeJedna(cidla, 6); // Zjištění otevření dveří
                int privolanePatro = 0; // Init proměnné pro přivolané patro
                bool osobaVeVytahu = !jeJedna(cidla, 7); // Zjištění podlahového čidla
                bool kabinoveTlacitko = false; // Zjištění, zda bylo stisknuto tlačítko v kabině
                //jedeVytah = false; // Zjištění, zda výtah jede
                int aktualniPatro = 1; // Zjištění aktuálního patra
                //smerBehu = 0; // Směr, kterým výtah jede (0 - dolu, 1 - nahoru)

                // Algoritmus pro zjištění přivolaného patra (default 0)
                for (int i = 1; i < 9; i++)
                {
                    if (!jeJedna(tlacitka, i) && i < 5) { privolanePatro = i; kabinoveTlacitko = false; }
                    if (!jeJedna(tlacitka, i) && i >= 5) { privolanePatro = i-4; kabinoveTlacitko = true; }
                }
                
                if (otevrenyDvere) // Pokud jsou otevřené dveře
                {
                    if (jedeVytah) // Pokud výtah jede => zastavit výtah a spustit zvukovou výstrahu
                    {
                        zastavitVytah();
                        zvukovaVystraha(true);
                        ledIndikaceSmeru(0, false);
                    } else zvukovaVystraha(false);
                } else zvukovaVystraha(false);

                cisloDisplej(privolanePatro);
                
                
                // Algoritmus chodu celeho vytahu

                algoritmusBehVytahu(privolanePatro, aktualniPatro, otevrenyDvere, kabinoveTlacitko, osobaVeVytahu);
                algoritmusDokonceniBehuVytahu(privolanePatro);
                algoritmusSvetlaVKabine(osobaVeVytahu, otevrenyDvere);
                
                
                // Debug vseho
                Console.WriteLine("Privolane patro: " + privolanePatro + "; kabina: " + kabinoveTlacitko + "; otevrene dvere: " + otevrenyDvere + "; osoba ve vytahu: " + osobaVeVytahu);



                //Console.WriteLine("Privolane patro: " + privolanePatro); // Debug pro privolane patro

                //Console.WriteLine(osobaVeVytahu ? "Ve vytahu je osoba" : "Vytah je prazdny"); // Debug pro podlahove cidlo



                //Console.WriteLine(jeJedna(tlacitka, 1) ? "Není stisknuto" : "Je stisknuto"); // Debug
                //Console.WriteLine(tlacitka); // Debug
                //Console.WriteLine(jeJedna(cidla, 6) ? "Dvere otevreny" : ""); // Debug
            }

        }

        static void algoritmusDokonceniBehuVytahu(int privolanePatro)
        {
            if (vytahVDanemPatre(privolanePatro)) // Pokud se vytah dostane do vyzadovaneho patra
            {
                zastavitVytah(); // Zastavime vytah
                ledIndikaceSmeru(0, false); // Vypneme indikace
            }
        }

        static void algoritmusBehVytahu(int privolanePatro, int aktualniPatro, bool otevrenyDvere, bool kabinoveTlacitko, bool osobaVeVytahu)
        {
            if (privolanePatro > 0) // Pokud je vytah privolan
            {
                if (!jedeVytah && !otevrenyDvere) // Pokud vytah stoji a dvere jsou zavrene
                {
                    // Pokud byl vytah zavolan z kabiny a ve vytahu je osoba NEBO byl vytah zavolan z patra
                    if ((kabinoveTlacitko && osobaVeVytahu) || !kabinoveTlacitko)
                    {
                        if (privolanePatro < aktualniPatro)
                        {
                            rozbehVytah(0); // Pokud se privolane patra nachazi pod aktualnim => rozbehneme vytah smerem dolu
                            ledIndikaceSmeru(0, true);
                        }

                        if (privolanePatro > aktualniPatro)
                        {
                            rozbehVytah(1); // Pokud se privolane patra nachazi nad aktualnim => rozbehneme vytah smerem nahoru
                            ledIndikaceSmeru(0, true);
                        }
                    }
                }
            }
        }

        static void algoritmusSvetlaVKabine(bool osobaVeVytahu, bool otevrenyDvere)
        {
            if (osobaVeVytahu || otevrenyDvere) svetloVKabine(true); // Pokud se ve vytahu nachazi osoba nebo jsou otevrene dvere => svitime
            else svetloVKabine(false); // Pokud ne => nesvitime
        }
        
        static void zastavitVytah() // Zastavime vytah
        {
            // Get current bits on port
            byte operace;
            port.Read(0, out operace);

            // Vypnout motor
            port.Write(0, (byte) nastavBit(operace, 0));

            jedeVytah = false;
        }
        
        static void rozbehVytah(int direction) // Rozjedeme vytah danym smerem
        {
            // Get current bits on port
            byte operace;
            port.Read(0, out operace);

            // Nastav smer
            if (direction == 0) port.Write(0, (byte) vymazBit(operace, 1)); // Dolu
            else port.Write(0, (byte) nastavBit(operace, 1)); // Nahoru

            jedeVytah = true;
        }
        
        static void ledIndikaceSmeru(int direction, bool stav) // Rozsvitime LED (nahoru/dolu) podle smeru
        {
            // Get current bits on port
            byte operace;
            port.Read(1, out operace);

            // Nastav smer
            if (stav) // Zapnuti
            {
                if (direction == 0) port.Write(0, (byte)vymazBit(operace, 2)); // Dolu
                else port.Write(0, (byte)vymazBit(operace, 1)); // Nahoru
            }
            else // Vypnuti
            {
                port.Write(0, (byte)nastavBit(operace, 2)); // Dolu
                port.Write(0, (byte)nastavBit(operace, 1)); // Nahoru
            }
        }

        static bool vytahVDanemPatre(int patro)
        {
            byte cidla;
            vstup.Read(1, out cidla);
            
            return !jeJedna(cidla, 8);
        }
        
        static void svetloVKabine(bool stav) // Zapnout nebo vypnout svetlo v kabine
        {
            // Get current bits
            byte operace;
            port.Read(0, out operace);
            
            // If stav is true => Turn on
            if (stav) port.Write(0, (byte) vymazBit(operace, 2));
            // If stav is false => Turn off
            else port.Write(0, (byte) nastavBit(operace, 2));
            
        }

        static void zvukovaVystraha(bool stav) // Turn on/off sound
        {
            // Get current bits on port
            byte operace;
            port.Read(0, out operace);

            // If stav is true => Turn on
            if (stav) port.Write(0, (byte) vymazBit(operace, 7));
            // If stav is false => Turn off
            else port.Write(0, (byte) nastavBit(operace, 7));
        }

        static void resetCisla() // Reset number display
        {
            byte operace; // Init var
            port.Read(0, out operace); // Read bits state
            port.Write(0, (byte)nastavBit(operace, 4)); // Set 4th bit on
            port.Read(0, out operace); // Read bits state
            port.Write(0, (byte)nastavBit(operace, 5)); // Set 5th bit on
            port.Read(0, out operace); // Read bits state
            port.Write(0, (byte)nastavBit(operace, 6)); // Set 6th bit on
        }

        static void cisloDisplej(int num) // Show number of floor on display
        {
            switch (num)
            {
                case 1:
                    resetCisla(); // Reset number
                    byte operace; // Init var
                    port.Read(0, out operace); // Read bits state
                    port.Write(0, (byte)vymazBit(operace, 4)); // Clear 4th bit
                    port.Read(0, out operace); // Read bits state
                    port.Write(0, (byte)vymazBit(operace, 5)); // Clear 5th bit
                    break;
                case 2:
                    resetCisla(); // Reset number
                    port.Read(0, out operace); // Read bits state
                    port.Write(0, (byte)vymazBit(operace, 4)); // Clear 4th bit
                    port.Read(0, out operace); // Read bits state
                    port.Write(0, (byte)vymazBit(operace, 6)); // Clear 6th bit
                    break;
                case 3:
                    resetCisla(); // Reset number
                    port.Read(0, out operace); // Read bits state
                    port.Write(0, (byte)vymazBit(operace, 4)); // Clear 4th bit
                    break;
                case 4:
                    resetCisla(); // Reset number
                    port.Read(0, out operace); // Read bits state
                    port.Write(0, (byte)vymazBit(operace, 5)); // Clear 5th bit
                    port.Read(0, out operace); // Read bits state
                    port.Write(0, (byte)vymazBit(operace, 6)); // Clear 6th bit
                    break;
            }
        }

        static bool jeJedna(int num, int bit) // Get state of bit
        {
            return (num & (1 << (bit - 1))) > 0;
        }

        static int nastavBit(int num, int bit) // Set bit on
        {
            num |= 1 << bit;

            return num;
        }

        static int vymazBit(int num, int bit) // Clear bit
        {
            num &= ~(1 << bit);

            return num;
        }
    }
}
